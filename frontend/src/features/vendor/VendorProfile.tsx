import { useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Button, Card, Label, TextField, SelectField, ReadonlyField, StatusPill, Spinner, Banner } from "../../ui";
import {
  useMe, useVendor, changeRequests, documents, qk, type Vendor, type ChangeDiff,
} from "../../api/vssClient";

type Kind = "text" | "select" | "readonly";
interface FieldDef {
  key: string; label: string; value: string; kind: Kind; options?: string[]; full?: boolean;
  /** Optional: field is shown only when this returns true for the current edited values. */
  showWhen?: (values: Record<string, string>) => boolean;
}

const TABS = [
  { id: "company", label: "Company" },
  { id: "contacts", label: "Contacts" },
  { id: "addresses", label: "Addresses" },
  { id: "banking", label: "Banking & remittance" },
  { id: "tax", label: "Tax & W-9" },
  { id: "documents", label: "Documents" },
  { id: "categories", label: "Category codes" },
];

const META: Record<string, { title: string; hint: string; section: string }> = {
  company: { title: "Company profile", hint: "Your legal business identity as it appears in the ERP.", section: "Company profile" },
  contacts: { title: "Contacts", hint: "People the City reaches for orders, payments and sales.", section: "Contacts" },
  addresses: { title: "Addresses", hint: "Where remittances and correspondence are sent.", section: "Addresses" },
  banking: { title: "Banking & remittance", hint: "EFT details. Changes always require City approval.", section: "Banking & remittance" },
  tax: { title: "Tax & W-9", hint: "Tax identification and classification on file.", section: "Tax & W-9" },
  documents: { title: "Documents & compliance", hint: "Upload and keep required documents current.", section: "Documents" },
  categories: { title: "Category codes", hint: "Commodity and NIGP codes you supply against.", section: "Category codes" },
};

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
    case "contacts": return [
      t("PrimaryContact", "Primary contact", v.contacts.primaryContact),
      t("PrimaryTitle", "Title", v.contacts.primaryTitle),
      t("PrimaryEmail", "Primary email", v.contacts.primaryEmail),
      t("PrimaryPhone", "Primary phone", v.contacts.primaryPhone),
      t("ApContactName", "AP contact name", v.contacts.apContactName),
      t("ApEmail", "AP email", v.contacts.apEmail),
      t("SalesContactName", "Sales contact name", v.contacts.salesContactName),
      t("SalesEmail", "Sales email", v.contacts.salesEmail),
    ];
    case "addresses": return [
      t("RemitStreet", "Remit-to street", v.address.remitStreet, true),
      t("RemitCity", "City", v.address.remitCity),
      sel("RemitState", "State", v.address.remitState, ["MT", "WA", "CA", "ID", "WY"]),
      t("RemitZip", "ZIP", v.address.remitZip),
      sel("RemitCountry", "Country", v.address.remitCountry, ["United States", "Canada"]),
      t("PhysicalAddress", "Physical address", v.address.physicalAddress, true),
    ];
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
    case "tax": return [
      t("LegalTaxName", "Legal tax name", v.tax.legalTaxName, true),
      sel("TaxIdType", "Tax ID type", v.tax.taxIdType, ["EIN", "SSN", "ITIN"]),
      t("Tin", "TIN / EIN", v.tax.tinMasked),
      sel("TaxClassification", "Tax classification", v.tax.taxClassification, ["S-Corporation", "C-Corporation", "LLC", "Individual"]),
      sel("ExemptPayee", "Exempt payee", v.tax.exemptPayee, ["No", "Yes"]),
      ro("W9OnFile", "W-9 on file", v.tax.w9OnFile),
    ];
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
    <AppShell title="My vendor record" crumb="Vendor Portal">
      <div style={{ display: "flex", gap: 22, alignItems: "flex-start" }}>
        {/* tab rail */}
        <div style={{ flex: "0 0 208px", background: "#fff", border: "1px solid var(--border-1)", borderRadius: 10, padding: 8, position: "sticky", top: 0 }}>
          {TABS.map((x) => {
            const active = x.id === tab;
            return (
              <button key={x.id} onClick={() => nav(`/profile/${x.id}`)} style={{
                width: "100%", textAlign: "left", padding: "10px 14px", border: "none", borderRadius: 6, cursor: "pointer", marginBottom: 2,
                background: active ? "var(--bg-accent-soft)" : "transparent", color: active ? "var(--color-teal-700)" : "var(--fg-1)",
                fontFamily: "var(--font-sans)", fontSize: 14, fontWeight: active ? 600 : 500,
              }}>{x.label}</button>
            );
          })}
        </div>

        {/* panel */}
        <div style={{ flex: 1, minWidth: 0 }}>
          <Card>
            <div style={{ padding: "20px 24px", borderBottom: "1px solid var(--border-1)" }}>
              <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 18 }}>{meta.title}</div>
              <div style={{ fontSize: 13, color: "var(--fg-2)", marginTop: 3 }}>{meta.hint}</div>
            </div>

            {tab === "documents" ? <DocumentsPanel vendor={vendor} />
              : tab === "categories" ? <CategoriesPanel vendor={vendor} />
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

  const submit = useMutation({
    mutationFn: () => changeRequests.create({ section, diffs }),
    onSuccess: onSubmitted,
  });

  return (
    <>
      <div style={{ padding: 24, display: "grid", gridTemplateColumns: "1fr 1fr", gap: "18px 22px" }}>
        {visible.map((f) => (
          <div key={f.key} style={f.full ? { gridColumn: "span 2" } : undefined}>
            <Label>{f.label}</Label>
            {f.kind === "readonly" ? <ReadonlyField value={f.value} />
              : f.kind === "select" ? (
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
        <div style={{ fontSize: 13, color: "var(--fg-2)" }}>
          {diffs.length === 0 ? "Changes are reviewed by City of Bozeman staff before syncing to the ERP."
            : `${diffs.length} field${diffs.length > 1 ? "s" : ""} changed — reviewed by City staff before ERP sync.`}
        </div>
        <div style={{ display: "flex", gap: 10 }}>
          <Button variant="outline" onClick={() => nav("/console")}>Cancel</Button>
          <Button variant="teal" disabled={diffs.length === 0 || submit.isPending} onClick={() => submit.mutate()}>
            {submit.isPending ? "Submitting…" : "Submit changes for review"}
          </Button>
        </div>
      </div>
    </>
  );
}

function DocumentsPanel({ vendor }: { vendor: Vendor }) {
  const qc = useQueryClient();
  const upload = useMutation({
    mutationFn: (name: string) => documents.upload({ name, fileRef: `${name.replace(/\W+/g, "_")}_2026.pdf` }),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.vendor }),
  });
  const cols = ["Document", "File", "Validity", "Status", ""];
  return (
    <div style={{ padding: "8px 0" }}>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr style={{ background: "var(--bg-2)" }}>
          {cols.map((c) => <th key={c} style={{ padding: "10px 24px", textAlign: "left", fontSize: 11, fontWeight: 600, textTransform: "uppercase", letterSpacing: ".1em", color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" }}>{c}</th>)}
        </tr></thead>
        <tbody>
          {vendor.documents.map((d) => (
            <tr key={d.id} style={{ borderBottom: "1px solid #F0F1F2" }}>
              <td style={{ padding: "14px 24px", fontSize: 14, fontWeight: 600 }}>{d.name}</td>
              <td style={{ padding: "14px 24px", fontSize: 14, color: "var(--fg-2)" }}>{d.fileRef ?? "—"}</td>
              <td style={{ padding: "14px 24px", fontSize: 14, color: "var(--fg-2)" }}>{d.validity}</td>
              <td style={{ padding: "14px 24px" }}><StatusPill status={d.status} /></td>
              <td style={{ padding: "14px 24px", textAlign: "right" }}>
                <Button variant="outline" style={{ padding: "7px 14px", fontSize: 13 }} disabled={upload.isPending} onClick={() => upload.mutate(d.name)}>Upload</Button>
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
