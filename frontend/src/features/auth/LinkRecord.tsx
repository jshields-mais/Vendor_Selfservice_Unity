import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AuthLayout } from "./AuthLayout";
import { Button } from "../../ui";
import { linkRequests, qk, type LinkMatchResult } from "../../api/vssClient";

const input = { width: "100%", padding: "11px 13px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 14, outline: "none" } as const;
const label = { display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6 } as const;

export function LinkRecord() {
  const nav = useNavigate();
  const qc = useQueryClient();
  const [method, setMethod] = useState<"vendor" | "tax">("vendor");
  const [match, setMatch] = useState<LinkMatchResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [vendorNumber, setVendorNumber] = useState("V-10485");
  const [pin, setPin] = useState("4820");
  const [taxId, setTaxId] = useState("81-3920423");
  const [zip, setZip] = useState("59715");

  const find = useMutation({
    mutationFn: () => linkRequests.create(
      method === "vendor"
        ? { method: "VendorNumberPin", vendorNumber, pin }
        : { method: "TaxIdZip", taxId, zip },
    ),
    onSuccess: (res) => {
      if (res.matched) { setMatch(res); setError(null); }
      else setError("We couldn't find a matching vendor record. Check your details and try again.");
    },
    onError: (e) => setError(e.message),
  });

  const confirm = useMutation({
    mutationFn: () => linkRequests.confirm(match!.linkRequestId!),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: qk.me });
      await qc.invalidateQueries({ queryKey: qk.vendor });
      nav("/link/success");
    },
    onError: (e) => setError(e.message),
  });

  const tabStyle = (active: boolean) => ({
    flex: 1, padding: "9px 8px", border: "none", borderRadius: 6, cursor: "pointer",
    fontFamily: "var(--font-sans)", fontSize: 13, fontWeight: 600,
    background: active ? "#fff" : "transparent", color: active ? "var(--color-navy)" : "var(--fg-2)",
    boxShadow: active ? "var(--shadow-1)" : "none",
  });

  return (
    <AuthLayout>
      <div style={{ display: "inline-flex", alignItems: "center", gap: 8, padding: "5px 12px", borderRadius: 999, background: "var(--colorStatusSuccessBackground1)", color: "var(--colorStatusSuccessForeground1)", fontSize: 12, fontWeight: 600, marginBottom: 18 }}>
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><path d="M20 6 9 17l-5-5" /></svg> Email verified
      </div>
      <h2 style={{ fontSize: 26, margin: 0 }}>Link your company record</h2>
      <p style={{ margin: "8px 0 20px", fontSize: 14, color: "var(--fg-2)", lineHeight: 1.6 }}>
        Match your account to your existing vendor record in the City's ERP.
      </p>

      {error && <div style={{ background: "var(--colorStatusDangerBackground1)", color: "var(--colorStatusDangerForeground1)", borderRadius: 8, padding: "10px 14px", fontSize: 13, marginBottom: 16 }}>{error}</div>}

      {!match ? (
        <>
          <div style={{ display: "flex", gap: 6, padding: 4, background: "var(--bg-2)", borderRadius: 8, marginBottom: 20 }}>
            <button style={tabStyle(method === "vendor")} onClick={() => setMethod("vendor")}>Vendor number + PIN</button>
            <button style={tabStyle(method === "tax")} onClick={() => setMethod("tax")}>Tax ID</button>
          </div>

          {method === "vendor" ? (
            <>
              <label style={label}>Vendor number</label>
              <input style={{ ...input, marginBottom: 16 }} value={vendorNumber} onChange={(e) => setVendorNumber(e.target.value)} />
              <label style={label}>Verification PIN</label>
              <input style={input} value={pin} onChange={(e) => setPin(e.target.value)} />
              <p style={{ fontSize: 12, color: "var(--fg-3)", margin: "8px 0 0" }}>Your vendor number and PIN are on your City of Bozeman invitation letter or a recent remittance.</p>
            </>
          ) : (
            <>
              <label style={label}>Federal Tax ID (EIN)</label>
              <input style={{ ...input, marginBottom: 16 }} value={taxId} onChange={(e) => setTaxId(e.target.value)} />
              <label style={label}>ZIP / postal code on file</label>
              <input style={input} value={zip} onChange={(e) => setZip(e.target.value)} />
            </>
          )}

          <Button variant="teal" style={{ width: "100%", marginTop: 22 }} disabled={find.isPending} onClick={() => find.mutate()}>
            {find.isPending ? "Searching…" : "Find my record"}
          </Button>
        </>
      ) : (
        <>
          <p style={{ fontSize: 13, color: "var(--fg-2)", margin: "0 0 12px" }}>We found a matching record. Confirm this is your company.</p>
          <div style={{ border: "1px solid var(--border-1)", borderRadius: 8, padding: 18, background: "var(--bg-2)" }}>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 18 }}>{match.vendorName}</div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px 20px", marginTop: 14, fontSize: 13 }}>
              <Meta label="Vendor #" value={match.vendorNumber} />
              <Meta label="Status" value={match.status} />
              <Meta label="Remit-to" value={`${match.remitCity}, ${match.remitState} ${match.remitZip}`} />
              <Meta label="Tax ID" value={match.tinMasked} />
            </div>
          </div>
          <div style={{ display: "flex", gap: 10, marginTop: 22 }}>
            <Button variant="outline" onClick={() => { setMatch(null); setError(null); }}>Not me</Button>
            <Button variant="teal" style={{ flex: 1 }} disabled={confirm.isPending} onClick={() => confirm.mutate()}>
              {confirm.isPending ? "Linking…" : "Confirm & link account"}
            </Button>
          </div>
        </>
      )}
    </AuthLayout>
  );
}

function Meta({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <div style={{ color: "var(--fg-3)", fontSize: 11, textTransform: "uppercase", letterSpacing: ".1em" }}>{label}</div>
      <div style={{ color: "var(--fg-1)", fontWeight: 600, marginTop: 2 }}>{value ?? "—"}</div>
    </div>
  );
}
