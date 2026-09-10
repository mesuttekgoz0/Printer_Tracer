"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { FiyatListesi, Option, TedarikciDetail } from "@/lib/types";
import { d, iso, parseFiyat, unit } from "@/lib/format";

export function TedarikciDetailClient({ id }: { id: number }) {
  const router = useRouter();
  const [data, setData] = useState<TedarikciDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<{ kind: "ok" | "err"; text: string } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await api.get<TedarikciDetail>(`/api/tedarikciler/${id}`));
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Bulunamadı." });
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  async function run(fn: () => Promise<unknown>, ok: string, then?: () => void) {
    try {
      await fn();
      setMsg({ kind: "ok", text: ok });
      then ? then() : await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "İşlem başarısız." });
    }
  }

  if (loading || !data) {
    return (
      <>
        <div className="page-head"><h1>Tedarikçi</h1></div>
        {msg && <div className="alert alert-err">{msg.text}</div>}
        {!msg && <p className="empty">Yükleniyor…</p>}
      </>
    );
  }

  return (
    <>
      <div className="page-head spread">
        <h1>{data.ad}</h1>
        <Link className="btn btn-sm" href="/tedarikciler">← Tedarikçiler</Link>
      </div>

      {msg && <div className={`alert ${msg.kind === "ok" ? "alert-ok" : "alert-err"}`} style={{ marginBottom: "var(--space-4)" }}>{msg.text}</div>}

      <div style={{ display: "grid", gridTemplateColumns: "minmax(260px, 1fr) minmax(320px, 1.4fr)", gap: "var(--space-4)", alignItems: "start" }}>
        <div className="stack">
          <InfoCard data={data} onSaved={load} onDeleted={() => router.push("/tedarikciler")} run={run} />
          <div className="card">
            <div className="card-head">Bağlı yazıcılar ({data.printers.length})</div>
            <div className="card-body p0">
              {data.printers.length === 0 ? (
                <p className="empty small">Bağlı yazıcı yok. Yazıcılar sayfasından atayın.</p>
              ) : (
                <table className="table">
                  <tbody>
                    {data.printers.map((p) => (
                      <tr key={p.id}>
                        <td>{p.name} <span className="muted small">· {p.ipAddress}</span></td>
                        <td style={{ textAlign: "right" }}><span className="tag">{p.turAd}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

        <div className="stack">
          <AddPriceList tedarikciId={id} turler={data.turler} onAdded={load} run={run} />
          <div className="card">
            <div className="card-head">Fiyat listeleri ({data.fiyatListeleri.length})</div>
            <div className="card-body p0">
              {data.fiyatListeleri.length === 0 ? (
                <p className="empty small neg">Fiyat listesi yok — hakediş tutarları 0 hesaplanır.</p>
              ) : (
                <div style={{ overflowX: "auto" }}>
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Liste adı</th>
                        <th>Tarih</th>
                        {data.turler.map((t) => (
                          <th key={t.id} className="num">{t.ad} ₺</th>
                        ))}
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {data.fiyatListeleri.map((f) => (
                        <PriceRow key={f.id} f={f} turler={data.turler} onSaved={load} run={run} />
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

function InfoCard({
  data,
  onSaved,
  onDeleted,
  run,
}: {
  data: TedarikciDetail;
  onSaved: () => void;
  onDeleted: () => void;
  run: (fn: () => Promise<unknown>, ok: string, then?: () => void) => Promise<void>;
}) {
  const [ad, setAd] = useState(data.ad);
  const [not, setNot] = useState(data.not ?? "");
  return (
    <div className="card">
      <div className="card-head">Tedarikçi bilgileri</div>
      <div className="card-body stack">
        <div className="field">
          <label>Ad</label>
          <input className="input" value={ad} onChange={(e) => setAd(e.target.value)} />
        </div>
        <div className="field">
          <label>Not</label>
          <input className="input" value={not} maxLength={500} onChange={(e) => setNot(e.target.value)} />
        </div>
        <div className="row-actions">
          <button
            className="btn btn-sm btn-primary"
            onClick={() => run(() => api.put(`/api/tedarikciler/${data.id}`, { ad, not }), "Tedarikçi güncellendi.", onSaved)}
          >
            Kaydet
          </button>
          <button
            className="btn btn-sm btn-danger"
            onClick={() => {
              if (confirm(`${data.ad} silinsin mi? Bağlı yazıcıların tedarikçi bağı kaldırılır, fiyat listeleri silinir.`)) {
                run(() => api.del(`/api/tedarikciler/${data.id}`), "Tedarikçi silindi.", onDeleted);
              }
            }}
          >
            Sil
          </button>
        </div>
      </div>
    </div>
  );
}

function AddPriceList({
  tedarikciId,
  turler,
  onAdded,
  run,
}: {
  tedarikciId: number;
  turler: Option[];
  onAdded: () => void;
  run: (fn: () => Promise<unknown>, ok: string, then?: () => void) => Promise<void>;
}) {
  const [listeAdi, setListeAdi] = useState("");
  const [tarih, setTarih] = useState(iso(new Date()));
  const [fiyat, setFiyat] = useState<Record<number, string>>({});

  return (
    <div className="card">
      <div className="card-head">Yeni fiyat listesi</div>
      <div className="card-body">
        <form
          className="form-grid"
          onSubmit={(e) => {
            e.preventDefault();
            run(
              () =>
                api.post(`/api/tedarikciler/${tedarikciId}/fiyat-listeleri`, {
                  listeAdi,
                  tarih,
                  fiyatlar: turler.map((t) => ({ turId: t.id, fiyat: parseFiyat(fiyat[t.id] ?? "") })),
                }),
              "Fiyat listesi eklendi.",
              () => {
                setListeAdi("");
                setFiyat({});
                onAdded();
              },
            );
          }}
        >
          <div className="field">
            <label>Liste adı</label>
            <input className="input" value={listeAdi} onChange={(e) => setListeAdi(e.target.value)} required placeholder="ör. 2026 sözleşme" />
          </div>
          <div className="field">
            <label>Geçerlilik tarihi</label>
            <input className="input" type="date" value={tarih} onChange={(e) => setTarih(e.target.value)} />
          </div>
          {turler.map((t) => (
            <div className="field" key={t.id}>
              <label>{t.ad} ₺/sayfa</label>
              <input
                className="input"
                inputMode="decimal"
                value={fiyat[t.id] ?? ""}
                onChange={(e) => setFiyat((s) => ({ ...s, [t.id]: e.target.value }))}
                placeholder="0,00"
              />
            </div>
          ))}
          <button type="submit" className="btn btn-sm btn-primary">Fiyat listesi ekle</button>
        </form>
        <p className="muted small" style={{ marginTop: "var(--space-2)" }}>
          Hakediş üretilirken dönem bitiş tarihine göre en güncel liste kullanılır. Ondalık için virgül veya nokta.
        </p>
      </div>
    </div>
  );
}

function PriceRow({
  f,
  turler,
  onSaved,
  run,
}: {
  f: FiyatListesi;
  turler: Option[];
  onSaved: () => void;
  run: (fn: () => Promise<unknown>, ok: string, then?: () => void) => Promise<void>;
}) {
  const [listeAdi, setListeAdi] = useState(f.listeAdi);
  const [tarih, setTarih] = useState(f.tarih.slice(0, 10));
  const [fiyat, setFiyat] = useState<Record<number, string>>(
    Object.fromEntries(turler.map((t) => [t.id, String(f.satirlar.find((s) => s.turId === t.id)?.sayfaBasiFiyat ?? 0)])),
  );

  return (
    <tr>
      <td>
        <input className="input select-sm" value={listeAdi} onChange={(e) => setListeAdi(e.target.value)} />
        {f.isCurrent && <span className="tag" style={{ marginTop: 4, display: "inline-block", background: "var(--color-accent-100)", color: "var(--color-accent-800)" }}>güncel</span>}
      </td>
      <td>
        <input className="input select-sm" type="date" value={tarih} onChange={(e) => setTarih(e.target.value)} />
      </td>
      {turler.map((t) => (
        <td key={t.id}>
          <input
            className="input select-sm num"
            inputMode="decimal"
            style={{ textAlign: "right", width: "5.5rem" }}
            value={fiyat[t.id] ?? ""}
            onChange={(e) => setFiyat((s) => ({ ...s, [t.id]: e.target.value }))}
          />
        </td>
      ))}
      <td>
        <div className="row-actions">
          <button
            className="btn btn-sm"
            onClick={() =>
              run(
                () =>
                  api.put(`/api/fiyat-listeleri/${f.id}`, {
                    listeAdi,
                    tarih,
                    fiyatlar: turler.map((t) => ({ turId: t.id, fiyat: parseFiyat(fiyat[t.id] ?? "") })),
                  }),
                "Fiyat listesi güncellendi.",
                onSaved,
              )
            }
          >
            Kaydet
          </button>
          <button
            className="btn btn-sm btn-danger"
            onClick={() => {
              if (confirm(`${f.listeAdi} listesi silinsin mi?`)) {
                run(() => api.del(`/api/fiyat-listeleri/${f.id}`), "Fiyat listesi silindi.", onSaved);
              }
            }}
          >
            Sil
          </button>
        </div>
        <div className="muted small" style={{ marginTop: 3 }}>
          {turler.map((t) => `${t.ad}: ${unit(parseFiyat(fiyat[t.id] ?? ""))}`).join(" · ")}
        </div>
      </td>
    </tr>
  );
}
