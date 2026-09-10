// .NET API DTO'larının TypeScript karşılıkları (Controllers/Api/*).

export interface Option {
  id: number;
  ad: string;
}

export interface Lookups {
  turler: Option[];
  tedarikciler: Option[];
}

/* ---- Yazıcılar ---- */
export interface Printer {
  id: number;
  name: string;
  ipAddress: string;
  model: string | null;
  turId: number | null;
  turAd: string;
  tedarikciId: number | null;
  tedarikciAd: string | null;
  readingCount: number;
  latestReadingUtc: string | null;
  latestCounter: number | null;
}

/* ---- Sayaç Oku ---- */
export interface ReadingStatus {
  printerId: number;
  printerName: string;
  ipAddress: string;
  model: string | null;
  turId: number | null;
  turAd: string;
  readingCount: number;
  latestCounter: number | null;
  latestReadingUtc: string | null;
  previousCounter: number | null;
  previousReadingUtc: string | null;
  delta: number | null;
  hasReading: boolean;
}

export interface ReadingsOverview {
  printerCount: number;
  lastReadingUtc: string | null;
  totalDelta: number;
  printers: ReadingStatus[];
}

export interface ReadResult {
  total: number;
  savedCount: number;
  failedNames: string[];
  totalDelta: number;
  success: boolean;
  message: string;
}

/* ---- Rapor ---- */
export interface ReadingLog {
  timestampUtc: string;
  printerId: number;
  printerName: string;
  pageCount: number;
  delta: number | null;
}

export interface PrinterTotal {
  printerId: number;
  name: string;
  total: number;
}

export interface ReportSummary {
  from: string;
  to: string;
  grandTotal: number;
  printerTotals: PrinterTotal[];
  readings: ReadingLog[];
  readingsTotal: number;
  showAll: boolean;
}

/* ---- Tedarikçiler ---- */
export interface TedarikciList {
  id: number;
  ad: string;
  not: string | null;
  printerCount: number;
  priceListCount: number;
  latestPriceListDate: string | null;
}

export interface TedarikciPrinter {
  id: number;
  name: string;
  ipAddress: string;
  turAd: string;
}

export interface FiyatSatiri {
  turId: number;
  turAd: string;
  sayfaBasiFiyat: number;
}

export interface FiyatListesi {
  id: number;
  listeAdi: string;
  tarih: string;
  isCurrent: boolean;
  satirlar: FiyatSatiri[];
}

export interface TedarikciDetail {
  id: number;
  ad: string;
  not: string | null;
  printers: TedarikciPrinter[];
  fiyatListeleri: FiyatListesi[];
  turler: Option[];
}

/* ---- Hakedişler ---- */
export interface HakedisList {
  id: number;
  number: string;
  tedarikciAd: string | null;
  createdUtc: string;
  periodStart: string;
  periodEnd: string;
  printerCount: number;
  totalPages: number;
  totalAmount: number;
}

export interface TedarikciSec {
  id: number;
  ad: string;
  printerCount: number;
  hasPriceList: boolean;
}

export interface TaslakRow {
  printerId: number;
  printerName: string;
  model: string | null;
  turId: number | null;
  turAd: string;
  previousCounter: number;
  currentCounter: number;
  previousReadingUtc: string | null;
  currentReadingUtc: string;
  unitPrice: number;
  firstHakedis: boolean;
}

export interface HakedisTaslak {
  tedarikciId: number;
  tedarikciAd: string;
  fiyatListesiBilgi: string | null;
  periodStart: string;
  periodEnd: string;
  rows: TaslakRow[];
  skipped: string[];
  warnings: string[];
}

export interface HakedisLine {
  printerName: string;
  model: string | null;
  turAd: string;
  previousCounter: number;
  currentCounter: number;
  pages: number | null;
  unitPrice: number;
  amount: number;
}

export interface HakedisDetail {
  id: number;
  number: string;
  tedarikciAd: string | null;
  createdUtc: string;
  periodStart: string;
  periodEnd: string;
  note: string | null;
  fromCompany: string | null;
  toCompany: string | null;
  lines: HakedisLine[];
  totalPages: number;
  totalAmount: number;
}

/* ---- SNMP Tanılama ---- */
export interface SnmpOidValue {
  oid: string;
  label: string | null;
  type: string | null;
  value: string | null;
  isError: boolean;
  brand: string | null;
}

export interface SnmpDiagnosticResult {
  ipAddress: string;
  reachable: boolean;
  sysDescr: string | null;
  sysName: string | null;
  detectedBrand: string | null;
  respondingVersion: string | null;
  messages: string[];
  probes: SnmpOidValue[];
  markerCounters: SnmpOidValue[];
  suggestedOid: string | null;
  suggestedValue: number | null;
  manualOid: string | null;
  manualOidResult: SnmpOidValue | null;
}
