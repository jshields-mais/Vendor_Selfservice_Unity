import { useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Button, Card, Label, TextField, SelectField, CodeSelectField, ReadonlyField, StatusPill, Spinner, Banner } from "../../ui";
import {
  useMe, useVendor, useDocumentTypes, useNotificationCatalog, useContactCodes, changeRequests, documents, qk, type Vendor, type ChangeDiff, type ContactCode, type Contact } from "../../api/vssClient";

type Kind = "text" | "select" | "codeselect" | "readonly";
interface FieldDef {
  key: string; label: string; value: string; kind: Kind; options?: string[]; full?: boolean;
  /** For kind "codeselect": the {code,label} options (value stored is the SAP code). */
  codeOptions?: { value: string; label: string }[];
  /** Optional: field is shown only when this returns true for the current edited values. */
  showWhen?: (values: Record<string, string>) => boolean;
  /** Optional: value must be non-empty to submit the section. */
  required?: boolean;
}

const META: Record<string, { title: string; hint: string; section: string }> = {
  company: { title: "Company profile", hint: "Your legal business identity as it appears in the ERP.", section: "Company profile" },
  contacts: { title: "Contacts", hint: "People the City reaches for orders, payments and sales.", section: "Contacts" },
  addresses: { title: "Addresses", hint: "Where remittances and correspondence are sent.", section: "Addresses" },
  banking: { title: "Banking & remittance", hint: "EFT details. Changes always require City approval.", section: "Banking & remittance" },
  tax: { title: "Tax & W-9", hint: "Tax identification on file.", section: "Tax & W-9" },
  documents: { title: "Documents & compliance", hint: "Upload and keep required documents current.", section: "Documents" },
  categories: { title: "Category codes", hint: "Commodity and NIGP codes you supply against.", section: "Category codes" },
  notifications: { title: "Notifications", hint: "Email recipients for the documents the City sends you.", section: "Notifications" } };

const t = (key: string, label: string, value?: string | null, full = false): FieldDef => ({ key, label, value: value ?? "", kind: "text", full });
const sel = (key: string, label: string, value: string | null | undefined, options: string[]): FieldDef => ({ key, label, value: value ?? "", kind: "select", options });
const ro = (key: string, label: string, value?: string | null): FieldDef => ({ key, label, value: value ?? "", kind: "readonly" });

function fieldsFor(tab: string, v: Vendor): FieldDef[] {
  switch (tab) {
    case "company": return [
      t("LegalName", "Legal business name", v.legalName, true),
      t("Dba", "DBA / trade name", v.dba),
      sel("EntityType", "Entity type", v.entityType, ["LLC", "Corporation", "Sole proprietor", "Partnership"]),
      t("Website", "Website", v.website),
      ro("Number", "Vendor number", v.number),
      ro("Status", "Portal status", `Linked · ${v.status}`),
    ];
    case "addresses": {
      // PO Box rule: a PO Box address shows the box number; a street address shows the
      // street + house number. City / state / ZIP / country apply to both.
      const isPo = (vals: Record<string, string>) => vals.AddressType === "PO Box";
      const isStreet = (vals: Record<string, string>) => vals.AddressType === "Street";
      return [
        sel("AddressType", "Address type", v.address.isPoBox ? "PO Box" : "Street", ["Street", "PO Box"]),
        { ...t("PoBox", "PO Box number", v.address.poBox), showWhen: isPo },
        { ...t("RemitStreet", "Remit-to street", v.address.remitStreet, true), showWhen: isStreet },
        { ...t("HouseNumber", "House / building no.", v.address.houseNumber), showWhen: isStreet },
        t("RemitCity", "City", v.address.remitCity),
        sel("RemitState", "State", v.address.remitState, ["MT", "WA", "CA", "ID", "WY", "IN", "NC"]),
        t("RemitZip", "ZIP", v.address.remitZip),
        sel("RemitCountry", "Country", v.address.remitCountry, ["United States", "Canada"]),
        t("PhysicalAddress", "Physical address", v.address.physicalAddress, true),
      ];
    }
    case "banking": {
      // Bank detail fields only apply to electronic payment methods (not Check).
      const needsBank = (vals: Record<string, string>) => vals.PaymentMethod !== "Check";
      return [
        sel("PaymentMethod", "Payment method", v.banking.paymentMethod, ["ACH / EFT", "Check", "Wire"]),
        { ...t("BankName", "Bank name", v.banking.bankName), showWhen: needsBank },
        { ...t("RoutingNumber", "ABA Routing Number", v.banking.routingNumberMasked), showWhen: needsBank },
        { ...t("AccountNumber", "Account number", v.banking.accountNumberMasked), showWhen: needsBank },
        { ...sel("AccountType", "Account type", v.banking.accountType, ["Checking", "Savings"]), showWhen: needsBank },
      ];
    }
    case "tax": {
      // "W-9 on file" reflects the actual W-9 document on the Documents tab (read-only).
      const w9 = v.documents.find((d) => (d.typeCode ?? "").toUpperCase() === "W9");
      const suffix = w9 && w9.validity && w9.validity !== "—" ? ` · ${w9.validity}` : "";
      const w9label = !w9 || !w9.fileRef ? "Not uploaded — see Documents tab"
        : w9.status === "AwaitingDocs" ? "Awaiting upload — see Documents tab"
        : w9.status === "PendingReview" ? "Uploaded — pending City review"
        : w9.status === "Expiring" ? `Expiring${suffix}`
        : `On file${suffix}`;
      return [
        t("LegalTaxName", "Legal tax name", v.tax.legalTaxName, true),
        sel("TaxIdType", "Tax ID type", v.tax.taxIdType, ["EIN", "SSN", "ITIN"]),
        t("Tin", "TIN / EIN", v.tax.tinMasked),
        ro("W9OnFile", "W-9 on file", w9label),
      ];
    }
    default: return [];
  }
}

export function VendorProfile() {
  const { tab = "company" } = useParams();
  const nav = useNavigate();
  const qc = useQueryClient();
  const { data: me } = useMe();
  const { data: vendor, isLoading } = useVendor(true);

  const meta = META[tab] ?? META.company;

  if (isLoading || !vendor) return <AppShell title="My vendor record" crumb="Vendor Portal"><Spinner /></AppShell>;
  if (me?.linkState !== "Linked") {
    return (
      <AppShell title="My vendor record" crumb="Vendor Portal">
        <Banner tone="warn">Your account isn't linked yet. <a href="/link">Link your company record</a> to edit your details.</Banner>
      </AppShell>
    );
  }

  return (
    <AppShell title="My vendor record" crumb="Vendor portal">
      <div style={{ maxWidth: 940 }}>
        <Card>
          <div style={{ padding: "20px 24px", borderBottom: "1px solid var(--border-1)" }}>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 18 }}>{meta.title}</div>
            <div style={{ fontSize: 13, color: "var(--fg-2)", marginTop: 3 }}>{meta.hint}</div>
          </div>

          {tab === "documents" ? <DocumentsPanel vendor={vendor} />
            : tab === "categories" ? <CategoriesPanel vendor={vendor} />
            : tab === "notifications" ? <NotificationsPanel vendor={vendor} onSubmitted={() => nav("/submitted")} />
            : tab === "contacts" ? <ContactsPanel vendor={vendor} onSubmitted={() => nav("/submitted")} />
            : <FieldEditor key={tab} tab={tab} vendor={vendor} section={meta.section}
                onSubmitted={async () => {
                  await Promise.all([
                    qc.invalidateQueries({ queryKey: qk.me }),
                    qc.invalidateQueries({ queryKey: qk.changeRequests }),
                  ]);
                  nav("/submitted");
                }} />}
        </Card>
      </div>
    </AppShell>
  );
}


function FieldEditor({ tab, vendor, section, onSubmitted }: { tab: string; vendor: Vendor; section: string; onSubmitted: () => void }) {
  const nav = useNavigate();
  const fields = useMemo(() => fieldsFor(tab, vendor), [tab, vendor]);
  const [values, setValues] = useState<Record<string, string>>(() => Object.fromEntries(fields.map((f) => [f.key, f.value])));

  // Fields whose showWhen predicate passes for the current values (e.g. bank details
  // only appear for electronic payment methods). Hidden fields never submit.
  const visible = fields.filter((f) => !f.showWhen || f.showWhen(values));

  const diffs: ChangeDiff[] = visible
    .filter((f) => f.kind !== "readonly" && values[f.key] !== f.value)
    .map((f) => ({ field: f.key, fromValue: f.value, toValue: values[f.key] }));

  // Required fields (e.g. first/last name) must be non-empty to submit.
  const missing = visible.filter((f) => f.required && !values[f.key]?.trim()).map((f) => f.label);

  const submit = useMutation({
    mutationFn: () => changeRequests.create({ section, diffs }),
    onSuccess: onSubmitted });

  return (
    <>
      <div style={{ padding: 24, display: "grid", gridTemplateColumns: "1fr 1fr", gap: "18px 22px" }}>
        {visible.map((f) => (
          <div key={f.key} style={f.full ? { gridColumn: "span 2" } : undefined}>
            <Label>{f.label}{f.required ? " *" : ""}</Label>
            {f.kind === "readonly" ? <ReadonlyField value={f.value} />
              : f.kind === "codeselect" ? (
                <CodeSelectField
                  options={f.codeOptions!}
                  value={values[f.key]}
                  onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
                />
              ) : f.kind === "select" ? (
                <SelectField
                  options={f.options!.includes(values[f.key]) ? f.options! : [values[f.key], ...f.options!]}
                  value={values[f.key]}
                  onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
                />
              ) : (
                <TextField value={values[f.key]} onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))} />
              )}
          </div>
        ))}
      </div>

      <div style={{ padding: "16px 24px", borderTop: "1px solid var(--border-1)", background: "var(--bg-2)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div style={{ fontSize: 13, color: missing.length ? "var(--colorStatusDangerForeground1)" : "var(--fg-2)" }}>
          {missing.length ? `${missing.join(" and ")} ${missing.length > 1 ? "are" : "is"} required.`
            : diffs.length === 0 ? "Changes are reviewed by City of Bozeman staff before syncing to the ERP."
            : `${diffs.length} field${diffs.length > 1 ? "s" : ""} changed — reviewed by City staff before ERP sync.`}
        </div>
        <div style={{ display: "flex", gap: 10 }}>
          <Button variant="outline" onClick={() => nav("/console")}>Cancel</Button>
          <Button variant="teal" disabled={diffs.length === 0 || missing.length > 0 || submit.isPending} onClick={() => submit.mutate()}>
            {submit.isPending ? "Submitting…" : "Submit changes for review"}
          </Button>
        </div>
      </div>
    </>
  );
}

function readAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error);
    reader.onload = () => {
      // strip the "data:<mime>;base64," prefix
      const result = String(reader.result);
      resolve(result.slice(result.indexOf(",") + 1));
    };
    reader.readAsDataURL(file);
  });
}

function DocumentsPanel({ vendor }: { vendor: Vendor }) {
  const qc = useQueryClient();
  const { data: types } = useDocumentTypes();
  const [pending, setPending] = useState<string | null>(null);
  const [typeCode, setTypeCode] = useState("");
  const upload = useMutation({
    mutationFn: async ({ code, file }: { code: string; file: File }) =>
      documents.upload({ typeCode: code, fileName: file.name, contentType: file.type || "application/pdf", contentBase64: await readAsBase64(file) }),
    onSuccess: () => {
      setTypeCode("");
      return Promise.all([
        qc.invalidateQueries({ queryKey: qk.me }),
        qc.invalidateQueries({ queryKey: qk.vendor }),
      ]);
    },
    onSettled: () => setPending(null) });

  const pick = (code: string) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "application/pdf";
    input.onchange = () => {
      const file = input.files?.[0];
      if (file) { setPending(code); upload.mutate({ code, file }); }
    };
    input.click();
  };

  const options = types ?? [];
  const existingCodes = new Set(vendor.documents.map((d) => d.typeCode).filter(Boolean));
  const duplicate = !!typeCode && existingCodes.has(typeCode);

  const cols = ["Document", "File", "Validity", "Status", ""];
  return (
    <div style={{ padding: "8px 0" }}>
      {/* New document upload — pick a configured document type, then attach a PDF. */}
      <div style={{ padding: "16px 24px", borderBottom: "1px solid var(--border-1)", background: "var(--bg-2)", display: "flex", gap: 12, alignItems: "flex-end", flexWrap: "wrap" }}>
        <div style={{ flex: "1 1 280px" }}>
          <Label>Add a new document</Label>
          <select
            value={typeCode}
            onChange={(e) => setTypeCode(e.target.value)}
            style={{ width: "100%", padding: "10px 12px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 13, color: "var(--fg-1)", outline: "none", fontFamily: "var(--font-sans)", background: "#fff" }}
          >
            <option value="">Select a document type…</option>
            {options.map((t) => <option key={t.code} value={t.code}>{t.description}</option>)}
          </select>
          {duplicate && <div style={{ fontSize: 12, color: "var(--fg-2)", marginTop: 4 }}>This document type already exists — uploading will replace it below.</div>}
        </div>
        <Button variant="teal" style={{ padding: "9px 16px", fontSize: 13 }} disabled={!typeCode || upload.isPending} onClick={() => pick(typeCode)}>
          {pending === typeCode && upload.isPending ? "Uploading…" : "+ Upload new document"}
        </Button>
      </div>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr style={{ background: "var(--bg-2)" }}>
          {cols.map((c) => <th key={c} style={{ padding: "10px 24px", textAlign: "left", fontSize: 11, fontWeight: 600, color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" }}>{c}</th>)}
        </tr></thead>
        <tbody>
          {vendor.documents.map((d) => (
            <tr key={d.id} style={{ borderBottom: "1px solid var(--colorNeutralStroke3)" }}>
              <td style={{ padding: "14px 24px", fontSize: 14, fontWeight: 600 }}>{d.name}</td>
              <td style={{ padding: "14px 24px", fontSize: 14, color: "var(--fg-2)" }}>{d.fileRef ?? "—"}</td>
              <td style={{ padding: "14px 24px", fontSize: 14, color: "var(--fg-2)" }}>{d.validity}</td>
              <td style={{ padding: "14px 24px" }}><StatusPill status={d.status} /></td>
              <td style={{ padding: "14px 24px", textAlign: "right" }}>
                <Button variant="outline" style={{ padding: "7px 14px", fontSize: 13 }} disabled={upload.isPending || !d.typeCode} onClick={() => d.typeCode && pick(d.typeCode)}>
                  {pending === d.typeCode && upload.isPending ? "Uploading…" : d.fileRef ? "Replace" : "Upload PDF"}
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function CategoriesPanel({ vendor }: { vendor: Vendor }) {
  return (
    <div style={{ padding: 24 }}>
      <Label>Selected commodity / NIGP codes</Label>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
        {vendor.categoryCodes.map((c) => (
          <span key={c} style={{ display: "inline-flex", alignItems: "center", gap: 8, padding: "7px 14px", borderRadius: 999, background: "var(--bg-accent-soft)", color: "var(--color-teal-700)", fontSize: 13, fontWeight: 600 }}>
            {c} <span style={{ color: "var(--color-teal)", cursor: "pointer" }}>×</span>
          </span>
        ))}
      </div>
      <button style={{ marginTop: 18, padding: "10px 16px", border: "1px dashed var(--border-2)", borderRadius: 6, background: "var(--bg-2)", color: "var(--fg-1)", fontFamily: "var(--font-sans)", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>+ Add category code</button>
    </div>
  );
}

const NOTIF_KINDS = ["To", "Cc", "Bcc"] as const;
const parseEmails = (text: string) =>
  text.split(/[,;\n\r]+/).map((s) => s.trim()).filter(Boolean).filter((v, i, a) => a.indexOf(v) === i);

/** Notifications: per-document (Remittance Advice Outbound / Purchase Order / Contract)
 *  To/CC/BCC email recipients. Reviewed, then written to the ERP communication section. */
function NotificationsPanel({ vendor, onSubmitted }: { vendor: Vendor; onSubmitted: () => void }) {
  const qc = useQueryClient();
  const { data: catalog } = useNotificationCatalog();

  // current[type][kind] = joined emails (", ")
  const current = useMemo(() => {
    const m: Record<string, Record<string, string>> = {};
    for (const n of vendor.notifications) {
      m[n.type] = { To: "", Cc: "", Bcc: "" };
      for (const k of NOTIF_KINDS) m[n.type][k] = n.recipients.filter((r) => r.kind === k).map((r) => r.email).join(", ");
    }
    return m;
  }, [vendor.notifications]);

  const [text, setText] = useState<Record<string, string>>({}); // key `${type}::${kind}` -> raw text
  const val = (type: string, kind: string) => text[`${type}::${kind}`] ?? current[type]?.[kind] ?? "";
  const setVal = (type: string, kind: string, v: string) => setText((t) => ({ ...t, [`${type}::${kind}`]: v }));

  const types = catalog?.types ?? [];
  const diffs: ChangeDiff[] = [];
  for (const ty of types) for (const k of NOTIF_KINDS) {
    const to = parseEmails(val(ty.name, k)).join(", ");
    const from = parseEmails(current[ty.name]?.[k] ?? "").join(", ");
    if (to !== from) diffs.push({ field: `${ty.name} · ${k}`, fromValue: from, toValue: to });
  }
  // A type with any recipients must have at least one To.
  const invalid = types.filter((ty) => {
    const any = NOTIF_KINDS.some((k) => parseEmails(val(ty.name, k)).length > 0);
    return any && parseEmails(val(ty.name, "To")).length === 0;
  }).map((ty) => ty.name);

  const submit = useMutation({
    mutationFn: () => changeRequests.create({ section: "Notifications", diffs }),
    onSuccess: async () => {
      await Promise.all([qc.invalidateQueries({ queryKey: qk.me }), qc.invalidateQueries({ queryKey: qk.changeRequests })]);
      onSubmitted();
    },
  });

  const box: React.CSSProperties = { width: "100%", minHeight: 54, padding: "7px 10px", border: "1px solid var(--colorNeutralStroke1)", borderRadius: "var(--radius-sm)", fontSize: 13, fontFamily: "var(--font-sans)", color: "var(--colorNeutralForeground1)", resize: "vertical" };

  return (
    <div style={{ padding: "8px 0" }}>
      {types.map((ty) => (
        <div key={ty.name} style={{ padding: "16px 24px", borderBottom: "1px solid var(--colorNeutralStroke3)" }}>
          <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 10 }}>
            <div style={{ fontSize: 15, fontWeight: 600 }}>{ty.name}</div>
            {!ty.erpEnabled && <div style={{ fontSize: 12, color: "var(--colorNeutralForeground3)" }}>Recorded in the portal — ERP sync pending configuration</div>}
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
            {NOTIF_KINDS.map((k) => (
              <div key={k}>
                <Label>{k === "To" ? "To *" : k.toUpperCase()}</Label>
                <textarea value={val(ty.name, k)} onChange={(e) => setVal(ty.name, k, e.target.value)}
                  placeholder="email@company.com (one per line or comma-separated)" style={box} />
              </div>
            ))}
          </div>
        </div>
      ))}
      <div style={{ padding: "16px 24px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div style={{ fontSize: 13, color: invalid.length ? "var(--colorStatusDangerForeground1)" : "var(--fg-2)" }}>
          {invalid.length ? `${invalid.join(", ")}: at least one To address is required.`
            : "Changes are reviewed by City staff before syncing to the ERP."}
        </div>
        <Button variant="primary" disabled={diffs.length === 0 || invalid.length > 0 || submit.isPending} onClick={() => submit.mutate()}>
          {submit.isPending ? "Submitting…" : "Submit notifications for review"}
        </Button>
      </div>
    </div>
  );
}

/** Contacts: a grid of the vendor's contacts + a side-sheet editor. Add/edit/delete each
 *  submit a change request; on approval the matching SAP ContactPerson is created/updated/deleted. */
function ContactsPanel({ vendor, onSubmitted }: { vendor: Vendor; onSubmitted: () => void }) {
  const { data: codes } = useContactCodes();
  const [editing, setEditing] = useState<Contact | "new" | null>(null);

  const label = (category: string, code?: string | null) =>
    (code ? codes?.find((c) => c.category === category && c.code === code)?.description ?? code : "");

  const contactPhone = (c: Contact) => c.phone || c.mobile || "";

  return (
    <div style={{ padding: "8px 0" }}>
      <div style={{ padding: "12px 24px 4px", display: "flex", justifyContent: "flex-end" }}>
        <Button variant="primary" onClick={() => setEditing("new")}>+ Add contact</Button>
      </div>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr style={{ background: "var(--bg-2)" }}>
          {["Name", "Department", "Email / Phone", ""].map((h) => (
            <th key={h} style={{ padding: "10px 24px", textAlign: "left", fontSize: 11, fontWeight: 600, color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" }}>{h}</th>
          ))}
        </tr></thead>
        <tbody>
          {vendor.contacts.length === 0 && (
            <tr><td colSpan={4} style={{ padding: "18px 24px", fontSize: 13, color: "var(--fg-3)" }}>No contacts yet. Add one to get started.</td></tr>
          )}
          {vendor.contacts.map((c) => (
            <tr key={c.id} style={{ borderBottom: "1px solid var(--colorNeutralStroke3)", cursor: "pointer" }} onClick={() => setEditing(c)}>
              <td style={{ padding: "12px 24px", fontSize: 13, color: "var(--fg-1)" }}>
                <span style={{ fontWeight: 600 }}>{[c.firstName, c.lastName].filter(Boolean).join(" ") || "—"}</span>
                {c.isPrimary && <span style={{ marginLeft: 8, fontSize: 11, fontWeight: 600, color: "var(--colorBrandForeground2)", background: "var(--colorBrandBackground2)", padding: "1px 7px", borderRadius: 999 }}>Primary</span>}
                {label("Function", c.function) && <div style={{ fontSize: 12, color: "var(--fg-3)" }}>{label("Function", c.function)}</div>}
              </td>
              <td style={{ padding: "12px 24px", fontSize: 13, color: "var(--fg-1)" }}>{label("Department", c.department) || "—"}</td>
              <td style={{ padding: "12px 24px", fontSize: 13, color: "var(--fg-1)" }}>
                <div>{c.email || "—"}</div>
                {contactPhone(c) && <div style={{ fontSize: 12, color: "var(--fg-3)" }}>{contactPhone(c)}</div>}
              </td>
              <td style={{ padding: "12px 24px", textAlign: "right", color: "var(--fg-3)", fontSize: 12 }}>Edit ›</td>
            </tr>
          ))}
        </tbody>
      </table>

      {editing && (
        <ContactSideSheet
          contact={editing === "new" ? null : editing}
          codes={codes ?? []}
          onClose={() => setEditing(null)}
          onSubmitted={onSubmitted}
        />
      )}
    </div>
  );
}

function ContactSideSheet({ contact, codes, onClose, onSubmitted }:
  { contact: Contact | null; codes: ContactCode[]; onClose: () => void; onSubmitted: () => void }) {
  const qc = useQueryClient();
  const [f, setF] = useState({
    firstName: contact?.firstName ?? "", lastName: contact?.lastName ?? "",
    title: contact?.title ?? "", function: contact?.function ?? "", department: contact?.department ?? "",
    email: contact?.email ?? "", phone: contact?.phone ?? "", mobile: contact?.mobile ?? "", fax: contact?.fax ?? "",
  });
  const set = (k: keyof typeof f, v: string) => setF((s) => ({ ...s, [k]: v }));

  const codeOpts = (category: string, cur: string) => {
    const opts = codes.filter((c) => c.category === category).map((c) => ({ value: c.code, label: c.description }));
    if (cur && !opts.some((o) => o.value === cur)) opts.unshift({ value: cur, label: `${cur} (inactive)` });
    return [{ value: "", label: "— None —" }, ...opts];
  };

  const missing = !f.firstName.trim() || !f.lastName.trim();
  const key = contact ? `contact:${contact.id}` : "contact:new";
  const payload = JSON.stringify(f);

  const save = useMutation({
    mutationFn: () => changeRequests.create({ section: "Contacts", diffs: [{ field: key, fromValue: contact ? "(existing)" : "", toValue: payload }] }),
    onSuccess: async () => { await Promise.all([qc.invalidateQueries({ queryKey: qk.me }), qc.invalidateQueries({ queryKey: qk.changeRequests })]); onSubmitted(); },
  });
  const remove = useMutation({
    mutationFn: () => changeRequests.create({ section: "Contacts", diffs: [{ field: key, fromValue: "(existing)", toValue: "" }] }),
    onSuccess: async () => { await Promise.all([qc.invalidateQueries({ queryKey: qk.me }), qc.invalidateQueries({ queryKey: qk.changeRequests })]); onSubmitted(); },
  });
  const busy = save.isPending || remove.isPending;

  return (
    <div onClick={onClose} style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.32)", zIndex: 50, display: "flex", justifyContent: "flex-end" }}>
      <div onClick={(e) => e.stopPropagation()} style={{ width: 460, maxWidth: "100%", height: "100%", background: "var(--colorNeutralBackground1)", boxShadow: "-8px 0 24px rgba(0,0,0,.18)", display: "flex", flexDirection: "column" }}>
        <div style={{ padding: "18px 22px", borderBottom: "1px solid var(--border-1)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 17 }}>{contact ? "Edit contact" : "Add contact"}</div>
          <button onClick={onClose} aria-label="Close" style={{ border: "none", background: "transparent", fontSize: 20, cursor: "pointer", color: "var(--fg-3)" }}>×</button>
        </div>

        <div style={{ flex: 1, overflowY: "auto", padding: "20px 22px", display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px 18px" }}>
          {/* Title on its own row */}
          <div style={{ gridColumn: "span 2" }}>
            <Label>Title</Label>
            <CodeSelectField options={codeOpts("Title", f.title)} value={f.title} onChange={(e) => set("title", e.target.value)} />
          </div>
          {/* First + last name on the row below */}
          <div><Label>First name *</Label><TextField value={f.firstName} onChange={(e) => set("firstName", e.target.value)} /></div>
          <div><Label>Last name *</Label><TextField value={f.lastName} onChange={(e) => set("lastName", e.target.value)} /></div>
          {/* Other fields organized below */}
          <div><Label>Function</Label><CodeSelectField options={codeOpts("Function", f.function)} value={f.function} onChange={(e) => set("function", e.target.value)} /></div>
          <div><Label>Department</Label><CodeSelectField options={codeOpts("Department", f.department)} value={f.department} onChange={(e) => set("department", e.target.value)} /></div>
          <div><Label>Email</Label><TextField value={f.email} onChange={(e) => set("email", e.target.value)} /></div>
          <div><Label>Phone</Label><TextField value={f.phone} onChange={(e) => set("phone", e.target.value)} /></div>
          <div><Label>Mobile</Label><TextField value={f.mobile} onChange={(e) => set("mobile", e.target.value)} /></div>
          <div><Label>Fax</Label><TextField value={f.fax} onChange={(e) => set("fax", e.target.value)} /></div>
        </div>

        <div style={{ padding: "14px 22px", borderTop: "1px solid var(--border-1)", background: "var(--bg-2)" }}>
          <div style={{ fontSize: 12, color: missing ? "var(--colorStatusDangerForeground1)" : "var(--fg-2)", marginBottom: 10 }}>
            {missing ? "First and last name are required." : "Changes are reviewed by City staff before syncing to the ERP."}
          </div>
          <div style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
            <div>
              {contact && <Button variant="danger" disabled={busy} onClick={() => { if (confirm("Submit removal of this contact for review?")) remove.mutate(); }}>
                {remove.isPending ? "Submitting…" : "Delete"}</Button>}
            </div>
            <div style={{ display: "flex", gap: 10 }}>
              <Button variant="outline" onClick={onClose}>Cancel</Button>
              <Button variant="primary" disabled={missing || busy} onClick={() => save.mutate()}>
                {save.isPending ? "Submitting…" : "Submit for review"}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
