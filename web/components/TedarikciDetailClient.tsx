"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Fiyat, FiyatDetay, Option, TedarikciDetail } from "@/lib/types";
import { d, iso, parseFiyat, unit } from "@/lib/format";

type Msg = { kind: "ok" | "err"; text: string } | null;
type Run = (fn: () => Promise<unknown>, ok: string, then?: () => void) => Promise<void>;

export function TedarikciDetailClient({ id }: { id: number }) {
  const router = useRouter();
  const [data, setData] = useState<TedarikciDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<Msg>(null);

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

  const run: Run = async (fn, ok, then) => {
    try {
      await fn();
      setMsg({ kind: "ok", text: ok });
      then ? then() : await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "İşlem başarısız." });
    }
  };

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

      <div style={{ display: "grid", gridTemplateColumns: "minmax(240px, 0.9fr) minmax(460px, 1.7fr)", gap: "var(--space-4)", alignItems: "start" }}>
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
          <AddFiyat tedarikciId={id} turler={data.turler} onAdded={load} run={run} />

          {data.fiyatlar.length === 0 ? (
            <div className="card">
              <div className="card-head">Fiyatlar</div>
              <div className="card-body">
                <p className="empty small neg" style={{ padding: "var(--space-3)" }}>
                  Fiyat tanımlı değil — bu tedarikçi için hakediş tutarları 0 hesaplanır.
                </p>
              </div>
            </div>
          ) : (
            data.fiyatlar.map((f) => (
              <FiyatKarti key={f.id} fiyat={f} onSaved={load} run={run} />
            ))
          )}
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
  run: Run;
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
              if (confirm(`${data.ad} silinsin mi? Bağlı yazıcıların tedarikçi bağı kaldırılır, fiyatları silinir.`)) {
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

function AddFiyat({
  tedarikciId,
  turler,
  onAdded,
  run,
}: {
  tedarikciId: number;
  turler: Option[];
  onAdded: () => void;
  run: Run;
}) {
  const today = new Date();
  const [turId, setTurId] = useState("");
  const [bas, setBas] = useState(iso(today));
  const [bit, setBit] = useState(iso(new Date(today.getFullYear() + 1, today.getMonth(), today.getDate())));
  const [fiyat, setFiyat] = useState("");

  return (
    <div className="card">
      <div className="card-head">Yeni fiyat</div>
      <div className="card-body">
        <form
          className="form-grid"
          onSubmit={(e) => {
            e.preventDefault();
            run(
              () =>
                api.post(`/api/tedarikciler/${tedarikciId}/fiyatlar`, {
                  turId: Number(turId),
                  baslangicTarihi: bas,
                  bitisTarihi: bit,
                  sayfaBasiFiyat: parseFiyat(fiyat),
                }),
              "Fiyat eklendi.",
              () => {
                setTurId("");
                setFiyat("");
                onAdded();
              },
            );
          }}
        >
          <div className="field">
            <label>Tür</label>
            <select className="select" value={turId} onChange={(e) => setTurId(e.target.value)} required>
              <option value="" disabled>— seçin —</option>
              {turler.map((t) => (
                <option key={t.id} value={t.id}>{t.ad}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Başlangıç tarihi</label>
            <input className="input" type="date" value={bas} onChange={(e) => setBas(e.target.value)} required />
          </div>
          <div className="field">
            <label>Bitiş tarihi</label>
            <input className="input" type="date" value={bit} onChange={(e) => setBit(e.target.value)} required />
          </div>
          <div className="field">
            <label>₺ / sayfa</label>
            <input className="input" inputMode="decimal" value={fiyat} onChange={(e) => setFiyat(e.target.value)} placeholder="0,00" required />
          </div>
          <button type="submit" className="btn btn-sm btn-primary">Ekle</button>
        </form>
        <p className="muted small" style={{ marginTop: "var(--space-2)" }}>
          Hakediş, dönem bitiş tarihini kapsayan aralığın fiyatını kullanır. Ondalık için virgül veya nokta.
        </p>
      </div>
    </div>
  );
}

function FiyatKarti({ fiyat, onSaved, run }: { fiyat: Fiyat; onSaved: () => void; run: Run }) {
  return (
    <div className="card">
      <div className="card-head">Fiyat — {fiyat.turAd}</div>
      <div className="card-body p0">
        <div style={{ overflowX: "auto" }}>
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: "1%" }} />
                <th>Başlangıç</th>
                <th>Bitiş</th>
                <th className="num">₺ / sayfa</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {fiyat.detaylar.map((det) => (
                <DetayRow key={det.id} det={det} onSaved={onSaved} run={run} />
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function DetayRow({ det, onSaved, run }: { det: FiyatDetay; onSaved: () => void; run: Run }) {
  const [bas, setBas] = useState(det.baslangicTarihi.slice(0, 10));
  const [bit, setBit] = useState(det.bitisTarihi.slice(0, 10));
  const [fiyat, setFiyat] = useState(String(det.sayfaBasiFiyat));

  const dirty = bas !== det.baslangicTarihi.slice(0, 10) || bit !== det.bitisTarihi.slice(0, 10) || parseFiyat(fiyat) !== det.sayfaBasiFiyat;

  return (
    <tr>
      <td style={{ verticalAlign: "middle" }}>
        {det.isCurrent && (
          <span
            className="tag"
            style={{ background: "var(--color-accent-100)", color: "var(--color-accent-800)", whiteSpace: "nowrap" }}
          >
            güncel
          </span>
        )}
      </td>
      <td>
        <input className="input select-sm" type="date" value={bas} onChange={(e) => setBas(e.target.value)} />
      </td>
      <td>
        <input className="input select-sm" type="date" value={bit} onChange={(e) => setBit(e.target.value)} />
      </td>
      <td className="num">
        <input
          className="input select-sm num"
          inputMode="decimal"
          style={{ textAlign: "right", width: "5.5rem" }}
          value={fiyat}
          onChange={(e) => setFiyat(e.target.value)}
        />
      </td>
      <td>
        <div className="row-actions">
          <button
            className="btn btn-sm"
            disabled={!dirty}
            onClick={() =>
              run(
                () =>
                  api.put(`/api/fiyat-detaylari/${det.id}`, {
                    baslangicTarihi: bas,
                    bitisTarihi: bit,
                    sayfaBasiFiyat: parseFiyat(fiyat),
                  }),
                "Fiyat güncellendi.",
                onSaved,
              )
            }
          >
            Kaydet
          </button>
          <button
            className="btn btn-sm btn-danger"
            onClick={() => {
              if (confirm(`${d(det.baslangicTarihi)} – ${d(det.bitisTarihi)} fiyatı silinsin mi?`)) {
                run(() => api.del(`/api/fiyat-detaylari/${det.id}`), "Fiyat silindi.", onSaved);
              }
            }}
          >
            Sil
          </button>
        </div>
        <div className="muted small" style={{ marginTop: 3 }}>{unit(parseFiyat(fiyat))} ₺</div>
      </td>
    </tr>
  );
}
