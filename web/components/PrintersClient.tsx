"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { DiscoveredPrinter, Lookups, Printer, SubnetsResponse } from "@/lib/types";

const tr = new Intl.NumberFormat("tr-TR");

function dt(iso: string | null): string {
  if (!iso) return "–";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

export function PrintersClient() {
  const [printers, setPrinters] = useState<Printer[]>([]);
  const [lookups, setLookups] = useState<Lookups>({ turler: [], tedarikciler: [] });
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<{ kind: "ok" | "err"; text: string } | null>(null);

  const [name, setName] = useState("");
  const [ip, setIp] = useState("");
  const [turId, setTurId] = useState("");
  const [tedarikciId, setTedarikciId] = useState("");
  const [adding, setAdding] = useState(false);
  const [scanOpen, setScanOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [p, l] = await Promise.all([
        api.get<Printer[]>("/api/printers"),
        api.get<Lookups>("/api/lookups"),
      ]);
      setPrinters(p);
      setLookups(l);
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı." });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function run(fn: () => Promise<unknown>, ok: string) {
    try {
      await fn();
      setMsg({ kind: "ok", text: ok });
      await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "İşlem başarısız." });
    }
  }

  async function addPrinter(e: React.FormEvent) {
    e.preventDefault();
    setAdding(true);
    await run(async () => {
      await api.post("/api/printers", {
        name,
        ipAddress: ip,
        turId: turId ? Number(turId) : null,
        tedarikciId: tedarikciId ? Number(tedarikciId) : null,
      });
      setName("");
      setIp("");
      setTurId("");
      setTedarikciId("");
    }, `${name} eklendi.`);
    setAdding(false);
  }

  return (
    <>
      <div className="page-head">
        <h1>Yazıcılar</h1>
        <p className="muted small">
          Ağa doğrudan bağlı yazıcıların listesi. Eklenen yazıcı yalnızca kaydedilir; ilk sayaç için Sayaç Oku sayfasını kullanın.
        </p>
      </div>

      {msg && <div className={`alert ${msg.kind === "ok" ? "alert-ok" : "alert-err"}`} style={{ marginBottom: "var(--space-4)" }}>{msg.text}</div>}

      <div className="card" style={{ marginBottom: "var(--space-8)" }}>
        <div className="card-head">
          <span>Yeni yazıcı ekle</span>
          <button type="button" className="btn btn-sm" onClick={() => setScanOpen(true)}>Ağı Tara</button>
        </div>
        <div className="card-body">
          <form className="form-grid" onSubmit={addPrinter}>
            <div className="field">
              <label htmlFor="p-name">Yazıcı adı</label>
              <input id="p-name" className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="ör. Muhasebe" required />
            </div>
            <div className="field">
              <label htmlFor="p-ip">IP adresi</label>
              <input id="p-ip" className="input tnum" value={ip} onChange={(e) => setIp(e.target.value)} placeholder="192.168.1.51" required />
            </div>
            <div className="field">
              <label htmlFor="p-tur">Tür</label>
              <select id="p-tur" className="select" value={turId} onChange={(e) => setTurId(e.target.value)}>
                <option value="">Belirtilmemiş</option>
                {lookups.turler.map((t) => (
                  <option key={t.id} value={t.id}>{t.ad}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="p-ted">Tedarikçi</label>
              <select id="p-ted" className="select" value={tedarikciId} onChange={(e) => setTedarikciId(e.target.value)}>
                <option value="">—</option>
                {lookups.tedarikciler.map((t) => (
                  <option key={t.id} value={t.id}>{t.ad}</option>
                ))}
              </select>
            </div>
            <button type="submit" className="btn btn-primary" disabled={adding}>Ekle</button>
          </form>
        </div>
      </div>

      <div className="card">
        <div className="card-head">
          <span>Kayıtlı yazıcılar</span>
          <span className="muted small">{printers.length} cihaz</span>
        </div>
        <div className="card-body p0">
          {loading ? (
            <p className="empty">Yükleniyor…</p>
          ) : printers.length === 0 ? (
            <p className="empty">Henüz kayıtlı yazıcı yok.</p>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table table-sticky-actions">
                <thead>
                  <tr>
                    <th>Ad</th>
                    <th>IP adresi</th>
                    <th>Tür</th>
                    <th>Tedarikçi</th>
                    <th className="num">Okuma</th>
                    <th>Son okuma</th>
                    <th className="num">Son sayaç</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {printers.map((p) => (
                    <PrinterRow key={p.id} p={p} lookups={lookups} run={run} />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {scanOpen && (
        <ScanModal
          onClose={() => {
            setScanOpen(false);
            load();
          }}
          onAdded={(count) => setMsg({ kind: "ok", text: `${count} yazıcı eklendi.` })}
        />
      )}
    </>
  );
}

function ScanModal({ onClose, onAdded }: { onClose: () => void; onAdded: (count: number) => void }) {
  const [subnets, setSubnets] = useState<SubnetsResponse | null>(null);
  const [cidr, setCidr] = useState("");
  const [scanning, setScanning] = useState(false);
  const [results, setResults] = useState<DiscoveredPrinter[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [added, setAdded] = useState<Set<string>>(new Set());

  useEffect(() => {
    api
      .get<SubnetsResponse>("/api/discovery/subnets")
      .then((s) => {
        setSubnets(s);
        setCidr(s.suggested ?? s.subnets.find((x) => x.scannable)?.cidr ?? "");
      })
      .catch((e) => setErr(e instanceof ApiError ? e.message : "Ağ bilgisi alınamadı."));
  }, []);

  async function scan() {
    setScanning(true);
    setErr(null);
    setResults(null);
    setAdded(new Set());
    try {
      setResults(await api.post<DiscoveredPrinter[]>("/api/discovery/scan", { cidr: cidr.trim() || null }));
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Tarama başarısız.");
    } finally {
      setScanning(false);
    }
  }

  async function add(d: DiscoveredPrinter) {
    try {
      await api.post("/api/printers", { name: d.sysName?.trim() || d.ipAddress, ipAddress: d.ipAddress });
      setAdded((s) => new Set(s).add(d.ipAddress));
      onAdded(1);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Eklenemedi.");
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-head">
          <span>Ağı Tara</span>
          <button type="button" aria-label="Kapat" onClick={onClose}>×</button>
        </div>
        <div className="modal-body">
          {err && <div className="alert alert-err" style={{ marginBottom: "var(--space-3)" }}>{err}</div>}

          <div className="form-grid" style={{ alignItems: "end" }}>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label htmlFor="scan-cidr">Taranacak ağ (CIDR)</label>
              <input
                id="scan-cidr"
                className="input tnum"
                value={cidr}
                onChange={(e) => setCidr(e.target.value)}
                placeholder="192.168.1.0/24"
                list="scan-subnets"
              />
              <datalist id="scan-subnets">
                {subnets?.subnets.filter((s) => s.scannable).map((s) => (
                  <option key={s.cidr} value={s.cidr}>{s.interfaceName} · sunucu {s.serverIp}</option>
                ))}
              </datalist>
            </div>
            <button type="button" className="btn btn-primary" onClick={scan} disabled={scanning || !cidr.trim()}>
              {scanning ? "Taranıyor…" : "Tara"}
            </button>
          </div>
          <p className="muted small" style={{ marginTop: "var(--space-2)" }}>
            Sunucuyla aynı ağdaki, SNMP açık yazıcıları bulur. /24 için ~5–10 sn.
          </p>
        </div>

        {results && (
          <div className="modal-body p0" style={{ borderTop: "1px solid var(--color-divider)", overflowX: "auto" }}>
            {results.length === 0 ? (
              <p className="empty">Bu ağda yazıcı bulunamadı.</p>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>IP</th>
                    <th>Ad (sysName)</th>
                    <th>Model</th>
                    <th>SNMP</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {results.map((d) => {
                    const isAdded = d.alreadyRegistered || added.has(d.ipAddress);
                    return (
                      <tr key={d.ipAddress}>
                        <td className="tnum"><code>{d.ipAddress}</code></td>
                        <td>
                          {d.sysName || <span className="muted">—</span>}
                          {!d.hasPageCounter && <span className="tag" style={{ marginLeft: 6 }}>sayaç yok</span>}
                        </td>
                        <td className="muted small" style={{ maxWidth: "18rem" }} title={d.sysDescr ?? ""}>
                          {d.sysDescr || d.detectedBrand || "–"}
                        </td>
                        <td className="muted small">{d.snmpVersion}</td>
                        <td>
                          <button
                            className="btn btn-sm btn-primary"
                            disabled={isAdded}
                            onClick={() => add(d)}
                          >
                            {isAdded ? "Ekli" : "Ekle"}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function PrinterRow({
  p,
  lookups,
  run,
}: {
  p: Printer;
  lookups: Lookups;
  run: (fn: () => Promise<unknown>, ok: string) => Promise<void>;
}) {
  const [name, setName] = useState(p.name);

  return (
    <tr>
      <td>
        <div className="row-actions">
          <input className="input select-sm" style={{ minWidth: "9rem" }} value={name} onChange={(e) => setName(e.target.value)} />
          <button
            className="btn btn-sm"
            disabled={name.trim() === p.name || !name.trim()}
            onClick={() => run(() => api.patch(`/api/printers/${p.id}/name`, { name }), "Yazıcı adı güncellendi.")}
          >
            Kaydet
          </button>
        </div>
      </td>
      <td className="muted"><code>{p.ipAddress}</code></td>
      <td>
        <select
          className="select select-sm"
          value={p.turId ?? ""}
          onChange={(e) =>
            run(() => api.patch(`/api/printers/${p.id}/type`, { turId: e.target.value ? Number(e.target.value) : null }), "Tür güncellendi.")
          }
        >
          <option value="">Belirtilmemiş</option>
          {lookups.turler.map((t) => (
            <option key={t.id} value={t.id}>{t.ad}</option>
          ))}
        </select>
      </td>
      <td>
        <select
          className="select select-sm"
          value={p.tedarikciId ?? ""}
          onChange={(e) =>
            run(
              () => api.patch(`/api/printers/${p.id}/supplier`, { tedarikciId: e.target.value ? Number(e.target.value) : null }),
              "Tedarikçi güncellendi.",
            )
          }
        >
          <option value="">—</option>
          {lookups.tedarikciler.map((t) => (
            <option key={t.id} value={t.id}>{t.ad}</option>
          ))}
        </select>
      </td>
      <td className="num">{tr.format(p.readingCount)}</td>
      <td className="muted small">{dt(p.latestReadingUtc)}</td>
      <td className="num">{p.latestCounter != null ? tr.format(p.latestCounter) : "–"}</td>
      <td>
        <div className="row-actions">
          <Link className="btn btn-sm" href={`/snmp-tanilama?ip=${encodeURIComponent(p.ipAddress)}`}>
            Test et
          </Link>
          <button
            className="btn btn-sm btn-danger"
            onClick={() => {
              if (confirm(`${p.name} (${p.ipAddress}) silinsin mi? Geçmiş okumaları da silinecek.`)) {
                run(() => api.del(`/api/printers/${p.id}`), `${p.name} silindi.`);
              }
            }}
          >
            Sil
          </button>
        </div>
      </td>
    </tr>
  );
}
