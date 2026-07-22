import type { CSSProperties, ButtonHTMLAttributes, InputHTMLAttributes, SelectHTMLAttributes, ReactNode } from "react";

/**
 * App-local presentational primitives styled with the Univerus (Fluent UI v2) tokens.
 * On the Univerus network these are the seam to swap for the real udp-* component
 * library; the token names/values already mirror Fluent so the swap is visual-only.
 */

type BtnVariant = "primary" | "teal" | "outline" | "ghost" | "success" | "danger";
const btnStyles: Record<BtnVariant, CSSProperties> = {
  // "primary"/"teal" are both the Fluent brand (communicationBlue) primary action.
  primary: { background: "var(--colorBrandBackground)", color: "#fff", border: "1px solid transparent" },
  teal: { background: "var(--colorBrandBackground)", color: "#fff", border: "1px solid transparent" },
  success: { background: "var(--colorStatusSuccessBackground3)", color: "#fff", border: "1px solid transparent" },
  danger: { background: "var(--colorStatusDangerBackground3)", color: "#fff", border: "1px solid transparent" },
  outline: { background: "var(--colorNeutralBackground1)", color: "var(--colorNeutralForeground1)", border: "1px solid var(--colorNeutralStroke1)" },
  ghost: { background: "transparent", color: "var(--colorNeutralForeground2)", border: "1px solid transparent" },
};

export function Button({
  variant = "primary", style, ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: BtnVariant }) {
  return (
    <button
      {...rest}
      style={{
        display: "inline-flex", alignItems: "center", justifyContent: "center", gap: 6,
        padding: "7px 14px", borderRadius: "var(--radius-md)", fontFamily: "var(--font-sans)",
        fontWeight: 600, fontSize: 14, lineHeight: "20px", cursor: rest.disabled ? "not-allowed" : "pointer",
        opacity: rest.disabled ? 0.55 : 1, ...btnStyles[variant], ...style,
      }}
    />
  );
}

export function Card({ children, style }: { children: ReactNode; style?: CSSProperties }) {
  // Fluent UdpCard: white surface, 1px hairline stroke, 4px radius, no shadow at rest.
  return (
    <div style={{ background: "var(--colorNeutralBackground1)", border: "1px solid var(--colorNeutralStroke2)", borderRadius: "var(--radius-md)", ...style }}>
      {children}
    </div>
  );
}

export function CardHeader({ title, hint, right }: { title: string; hint?: string; right?: ReactNode }) {
  return (
    <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--colorNeutralStroke2)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
      <div>
        <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 16, lineHeight: "22px", color: "var(--colorNeutralForeground1)" }}>{title}</div>
        {hint && <div style={{ fontSize: 12, color: "var(--colorNeutralForeground3)", marginTop: 2 }}>{hint}</div>}
      </div>
      {right}
    </div>
  );
}

// Fluent UdpBadge palette (background2 / foreground2 pairs) per the design system.
const PILL_COLORS: Record<string, [string, string]> = {
  approved: ["#cdedb6", "#0e700e"], active: ["#cdedb6", "#0e700e"], linked: ["#cdedb6", "#0e700e"],
  current: ["#cdedb6", "#0e700e"], synced: ["#cdedb6", "#0e700e"], reviewed: ["#cdedb6", "#0e700e"], verified: ["#cdedb6", "#0e700e"],
  submitted: ["#cfe4fa", "#0f548c"], sent: ["#cfe4fa", "#0f548c"], matched: ["#cfe4fa", "#0f548c"],
  pendingreview: ["#fef7b2", "#817400"], inreview: ["#fef7b2", "#817400"], underreview: ["#fef7b2", "#817400"],
  awaitingdocs: ["#fef7b2", "#817400"], expiring: ["#fef7b2", "#817400"], pendinglink: ["#fef7b2", "#817400"],
  pending: ["#ead8f9", "#6624a8"],
  rejected: ["#f9d6d6", "#b10e1c"], expired: ["#f9d6d6", "#b10e1c"], error: ["#f9d6d6", "#b10e1c"], pastdue: ["#f9d6d6", "#b10e1c"],
  closed: ["#e0e0e0", "#424242"], draft: ["#e0e0e0", "#424242"],
};

export function StatusPill({ status }: { status: string }) {
  const key = status.toLowerCase().replace(/[^a-z]/g, "");
  const [bg, fg] = PILL_COLORS[key] ?? ["#e0e0e0", "#424242"];
  return (
    <span style={{ display: "inline-block", padding: "2px 8px", borderRadius: "var(--radius-pill)", fontSize: 12, lineHeight: "16px", fontWeight: 600, background: bg, color: fg, whiteSpace: "nowrap" }}>
      {status}
    </span>
  );
}

// Fluent UdpMessageBar.
export function Banner({ tone = "info", children }: { tone?: "info" | "success" | "warn" | "danger"; children: ReactNode }) {
  const map = {
    info: ["var(--colorStatusInfoBackground1)", "var(--colorStatusInfoForeground1)", "var(--colorStatusInfoBorder1)"],
    success: ["var(--colorStatusSuccessBackground1)", "var(--colorStatusSuccessForeground1)", "var(--colorStatusSuccessBorder1)"],
    warn: ["var(--colorStatusWarningBackground1)", "var(--colorStatusWarningForeground1)", "var(--colorStatusWarningBorder1)"],
    danger: ["var(--colorStatusDangerBackground1)", "var(--colorStatusDangerForeground1)", "var(--colorStatusDangerBorder1)"],
  }[tone];
  return (
    <div style={{ background: map[0], color: map[1], border: `1px solid ${map[2]}`, borderRadius: "var(--radius-md)", padding: "8px 12px", fontSize: 14, lineHeight: "20px", marginBottom: 16 }}>
      {children}
    </div>
  );
}

export function Label({ children }: { children: ReactNode }) {
  // Fluent field label: 12px, above the input, sentence/Title case (never CSS-uppercased).
  return (
    <label style={{ display: "block", fontSize: 12, fontWeight: 500, lineHeight: "16px", color: "var(--colorNeutralForeground2)", marginBottom: 4 }}>
      {children}
    </label>
  );
}

const fieldBox: CSSProperties = {
  width: "100%", padding: "7px 10px", border: "1px solid var(--colorNeutralStroke1)",
  borderRadius: "var(--radius-sm)", fontSize: 14, lineHeight: "20px", color: "var(--colorNeutralForeground1)",
  outline: "none", background: "var(--colorNeutralBackground1)",
};

export function TextField(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} style={{ ...fieldBox, ...props.style }} />;
}

export function SelectField({ options, ...rest }: SelectHTMLAttributes<HTMLSelectElement> & { options: string[] }) {
  return (
    <select {...rest} style={{ ...fieldBox, ...rest.style }}>
      {options.map((o) => <option key={o} value={o}>{o}</option>)}
    </select>
  );
}

export function ReadonlyField({ value }: { value?: string | null }) {
  return (
    <div style={{ ...fieldBox, background: "var(--colorNeutralBackground3)", color: "var(--colorNeutralForeground2)" }}>{value ?? "—"}</div>
  );
}

export function Spinner({ label = "Loading…" }: { label?: string }) {
  return <div style={{ padding: 40, color: "var(--colorNeutralForeground3)", fontSize: 14 }}>{label}</div>;
}
