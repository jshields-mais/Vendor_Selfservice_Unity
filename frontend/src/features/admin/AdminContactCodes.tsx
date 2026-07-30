import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Card, Button, Spinner } from "../../ui";
import { useAdminContactCodes, adminApi, adminQk, type ContactCodeUpsert } from "../../api/adminClient";
import type { ContactCode } from "../../api/vssClient";

const inputStyle = { padding: "8px 10px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 13, color: "var(--fg-1)", outline: "none", fontFamily: "var(--font-sans)", width: "100%" } as const;

/** The three coded contact lists and the SAP field each drives. */
const LISTS: { category: string; title: string; sapField: string; codeHint: string; descHint: string }[] = [
  { category: "Title", title: "Title (form of address)", sapField: "SAP FormOfAddressCode", codeHint: "0001", descHint: "Mr." },
  { category: "Department", title: "Department", sapField: "SAP BusinessPartnerFunctionalAreaCode", codeHint: "0002", descHint: "Sales" },
  { category: "Function", title: "Function", sapField: "SAP BusinessPartnerFunctionTypeCode", codeHint: "0016", descHint: "Buyer" },
];

export function AdminContactCodes() {
  const qc = useQueryClient();
  const { data: codes, isLoading } = useAdminContactCodes();
  const invalidate = () => qc.invalidateQueries({ queryKey: adminQk.contactCodes });
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: (body: ContactCodeUpsert) => adminApi.createContactCode(body),
    onSuccess: () => { setError(null); invalidate(); },
    onError: (e: Error) => setError(e.message) });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: ContactCodeUpsert }) => adminApi.updateContactCode(id, body),
    onSuccess: invalidate, onError: (e: Error) => setError(e.message) });
  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteContactCode(id),
    onSuccess: invalidate, onError: (e: Error) => setError(e.message) });

  if (isLoading || !codes) return <AppShell title="Contact codes" crumb="Administration"><Spinner /></AppShell>;

  return (
    <AppShell title="Contact codes" crumb="Administration">
      <p style={{ fontSize: 13, color: "var(--fg-2)", margin: "0 0 14px", maxWidth: 680 }}>
        These drive the Title, Department and Function dropdowns on the vendor Contacts tab. Each
        code is the value written to and read from SAP; the description is the label vendors see.
        Inactive codes are hidden from vendors but kept for existing contact values.
      </p>
      {error && <Card style={{ padding: "10px 14px", marginBottom: 12, color: "var(--colorStatusDangerForeground1)", fontSize: 13 }}>{error}</Card>}

      {LISTS.map((list) => (
        <div key={list.category} style={{ marginBottom: 22 }}>
          <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 8 }}>
            <h2 style={{ font: "600 16px/22px var(--font-display)", color: "var(--fg-1)", margin: 0 }}>{list.title}</h2>
            <span style={{ fontSize: 12, color: "var(--fg-3)", fontFamily: "var(--font-mono)" }}>{list.sapField}</span>
          </div>
          <ListCard
            list={list}
            rows={codes.filter((c) => c.category === list.category)}
            onCreate={(body) => create.mutate(body)}
            onSave={(id, body) => update.mutate({ id, body })}
            onDelete={(id) => remove.mutate(id)}
            busy={create.isPending || update.isPending || remove.isPending}
          />
        </div>
      ))}
    </AppShell>
  );
}

function ListCard({ list, rows, onCreate, onSave, onDelete, busy }: {
  list: { category: string; codeHint: string; descHint: string };
  rows: ContactCode[];
  onCreate: (b: ContactCodeUpsert) => void;
  onSave: (id: string, b: ContactCodeUpsert) => void;
  onDelete: (id: string) => void;
  busy: boolean;
}) {
  const [code, setCode] = useState("");
  const [desc, setDesc] = useState("");
  const [order, setOrder] = useState(0);

  return (
    <Card>
      <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--border-1)", background: "var(--bg-2)", display: "grid", gridTemplateColumns: "140px 1fr 100px auto", gap: 12, alignItems: "end" }}>
        <div><Lbl>Code</Lbl><input style={inputStyle} placeholder={list.codeHint} value={code} onChange={(e) => setCode(e.target.value)} /></div>
        <div><Lbl>Description</Lbl><input style={inputStyle} placeholder={list.descHint} value={desc} onChange={(e) => setDesc(e.target.value)} /></div>
        <div><Lbl>Order</Lbl><input style={inputStyle} type="number" value={order} onChange={(e) => setOrder(Number(e.target.value))} /></div>
        <Button variant="teal" style={{ padding: "9px 16px", fontSize: 13 }} disabled={!code.trim() || !desc.trim() || busy}
          onClick={() => { onCreate({ category: list.category, code: code.trim(), description: desc.trim(), isActive: true, sortOrder: order }); setCode(""); setDesc(""); setOrder(0); }}>
          + Add code
        </Button>
      </div>

      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr style={{ background: "var(--bg-2)" }}>
          {["Code", "Description", "Order", "Active", ""].map((c) => <th key={c} style={th}>{c}</th>)}
        </tr></thead>
        <tbody>
          {rows.length === 0 && <tr><td style={{ ...td, color: "var(--fg-3)" }} colSpan={5}>No codes yet.</td></tr>}
          {rows.map((r) => <Row key={r.id} row={r} onSave={(b) => onSave(r.id, b)} onDelete={() => onDelete(r.id)} busy={busy} />)}
        </tbody>
      </table>
    </Card>
  );
}

function Row({ row, onSave, onDelete, busy }: { row: ContactCode; onSave: (b: ContactCodeUpsert) => void; onDelete: () => void; busy: boolean }) {
  const [desc, setDesc] = useState(row.description);
  const [order, setOrder] = useState(row.sortOrder);
  const [active, setActive] = useState(row.isActive);
  const dirty = desc !== row.description || order !== row.sortOrder || active !== row.isActive;

  return (
    <tr style={{ borderBottom: "1px solid var(--colorNeutralStroke3)", opacity: row.isActive ? 1 : 0.55 }}>
      <td style={{ ...td, fontFamily: "var(--font-mono)", fontWeight: 600 }}>{row.code}</td>
      <td style={td}><input style={inputStyle} value={desc} onChange={(e) => setDesc(e.target.value)} /></td>
      <td style={{ ...td, width: 90 }}><input style={inputStyle} type="number" value={order} onChange={(e) => setOrder(Number(e.target.value))} /></td>
      <td style={{ ...td, width: 80 }}><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /></td>
      <td style={{ ...td, textAlign: "right", whiteSpace: "nowrap" }}>
        <Button variant="outline" style={{ padding: "6px 12px", fontSize: 12, marginRight: 8 }} disabled={!dirty || busy}
          onClick={() => onSave({ category: row.category, code: row.code, description: desc.trim(), isActive: active, sortOrder: order })}>Save</Button>
        <Button variant="danger" style={{ padding: "6px 12px", fontSize: 12 }} disabled={busy} onClick={onDelete}>Delete</Button>
      </td>
    </tr>
  );
}

function Lbl({ children }: { children: React.ReactNode }) {
  return <div style={{ fontSize: 11, fontWeight: 600, color: "var(--fg-2)", marginBottom: 5 }}>{children}</div>;
}

const th = { padding: "10px 20px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "12px 20px", fontSize: 13, color: "var(--fg-1)" };
