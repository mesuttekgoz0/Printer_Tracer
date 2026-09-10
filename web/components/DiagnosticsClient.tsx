"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { SnmpDiagnosticResult, SnmpOidValue } from "@/lib/types";

export function DiagnosticsClient() {
  const [ip, setIp] = useState("");

  useEffect(() => {
    const q = new URLSearchParams(window.location.search).get("ip");
    if (q) setIp(q);
  }, []);

  const [oid, setOid] = useState("");
  const [res, setRes] = useState<SnmpDiagnosticResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function check(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    setRes(null);
    try {
      const q = new URLSearchParams({ ip });
      if (oid.trim()) q.set("oid", oid.trim());
      setRes(await api.get<SnmpDiagnosticResult>(`/api/diagnostics?${q}`));
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Sorgu başarısız.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>SNMP Tanılama</h1>
        <p className="muted small">
          Bir IP'ye SNMP GET/WALK yapar; sysDescr, sysName, marka ve doğru sayfa-sayacı OID'ini önerir.
        </p>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "minmax(260px, 1fr) minmax(320px, 1.5fr)", gap: "var(--space-6)", alignItems: "start" }}>
        <div className="card">
          <div className="card-body">
            <form className="stack" onSubmit={check}>
              <div className="field">
                <label htmlFor="d-ip">IP adresi</label>
                <input id="d-ip" className="input" value={ip} onChange={(e) => setIp(e.target.value)} placeholder="192.168.1.51" required />
              </div>
              <div className="field">
                <label htmlFor="d-oid">Elle OID (opsiyonel)</label>
                <input id="d-oid" className="input" value={oid} onChange={(e) => setOid(e.target.value)} placeholder="1.3.6.1.2.1.43.10.2.1.4.1.1" />
              </div>
              <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? "Sorgulanıyor…" : "Sorgula"}</button>
            </form>
          </div>
        </div>

        <div>
          {err && <div className="alert alert-err">{err}</div>}
          {!err && !res && <p className="muted" style={{ fontStyle: "italic" }}>Bir sorgu bekleniyor.</p>}
          {res && (
            <div className="stack">
              <div className="card">
                <div className="card-body stack">
                  <div>
                    <span className="tag" style={res.reachable ? { background: "var(--color-accent-100)", color: "var(--color-accent-800)" } : {}}>
                      {res.reachable ? "Ulaşılabilir" : "Ulaşılamıyor"}
                    </span>
                  </div>
                  <KV k="IP" v={res.ipAddress} />
                  <KV k="SNMP sürümü" v={res.respondingVersion ?? "–"} />
                  <KV k="sysName" v={res.sysName ?? "–"} />
                  <KV k="sysDescr" v={res.sysDescr ?? "–"} />
                  <KV k="Marka" v={res.detectedBrand ?? "–"} />
                  {res.suggestedOid && (
                    <div className="alert alert-ok">
                      Önerilen sayaç OID: <code>{res.suggestedOid}</code>{res.suggestedValue != null && <> = <strong>{res.suggestedValue}</strong></>}
                    </div>
                  )}
                </div>
              </div>

              {res.messages.length > 0 && (
                <div className="card">
                  <div className="card-head">Mesajlar</div>
                  <div className="card-body">
                    <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>
                      {res.messages.map((m, i) => <li key={i}>{m}</li>)}
                    </ul>
                  </div>
                </div>
              )}

              {res.manualOid && (
                res.manualOidResult ? (
                  <OidTable title={`Elle OID: ${res.manualOid}`} rows={[res.manualOidResult]} />
                ) : (
                  <div className="card">
                    <div className="card-head">Elle OID: <code>{res.manualOid}</code></div>
                    <div className="card-body">
                      <p className="small neg" style={{ margin: 0 }}>
                        Yanıt yok — bu OID cihazda mevcut değil (NoSuchObject/NoSuchInstance),
                        cihaz SNMP'ye yanıt vermedi ya da <code>{"\""}public{"\""}</code> community ile okunamıyor.
                      </p>
                    </div>
                  </div>
                )
              )}
              {res.probes.length > 0 && <OidTable title="Denenen bilinen OID'ler" rows={res.probes} />}
              {res.markerCounters.length > 0 && <OidTable title="prtMarkerLifeCount alt ağacı" rows={res.markerCounters} suggested={res.suggestedOid} />}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

function KV({ k, v }: { k: string; v: string }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "120px 1fr", gap: "var(--space-3)", padding: "var(--space-1) 0", borderBottom: "1px solid var(--color-divider)" }}>
      <span className="muted small">{k}</span>
      <span className="small" style={{ wordBreak: "break-word" }}>{v}</span>
    </div>
  );
}

function OidTable({ title, rows, suggested }: { title: string; rows: SnmpOidValue[]; suggested?: string | null }) {
  return (
    <div className="card">
      <div className="card-head">{title}</div>
      <div className="card-body p0">
        <div style={{ overflowX: "auto" }}>
          <table className="table">
            <thead>
              <tr>
                <th>OID</th>
                <th>Etiket</th>
                <th>Tür</th>
                <th>Değer</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i} style={suggested && r.oid === suggested ? { background: "var(--color-accent-100)" } : {}}>
                  <td><code>{r.oid}</code></td>
                  <td className="muted small">{r.label ?? "–"}{r.brand ? ` (${r.brand})` : ""}</td>
                  <td className="muted small">{r.type ?? "–"}</td>
                  <td className={r.isError ? "neg" : ""}>{r.value ?? "–"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
