"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const links = [
  { href: "/sayac-oku", label: "Sayaç Oku" },
  { href: "/rapor", label: "Rapor" },
  { href: "/hakedisler", label: "Hakedişler" },
  { href: "/tedarikciler", label: "Tedarikçiler" },
  { href: "/yazicilar", label: "Yazıcılar" },
  { href: "/snmp-tanilama", label: "SNMP Tanılama" },
];

function toggleTheme() {
  const el = document.documentElement;
  const next = el.getAttribute("data-theme") === "dark" ? "light" : "dark";
  el.setAttribute("data-theme", next);
  try {
    localStorage.setItem("yt-theme", next);
  } catch {
    /* yok say */
  }
}

export function Nav() {
  const path = usePathname();
  return (
    <nav className="nav">
      <Link href="/yazicilar" className="brand">
        <svg
          className="brand-mark"
          viewBox="0 0 58 58"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.3"
          strokeLinejoin="round"
          aria-hidden
        >
          <path d="M17 5h24v13H17z" />
          <path d="M9 18h40v20H9z" />
          <path d="M17 38h24v15H17z" fill="var(--color-bg)" />
          <path d="M23 44h12M23 49h8" stroke="var(--brand-accent, var(--color-accent))" strokeWidth="2.4" />
          <circle cx="14.5" cy="26" r="2" fill="currentColor" stroke="none" />
        </svg>
        Yazıcı Takip
      </Link>

      <button
        type="button"
        className="theme-toggle"
        aria-label="Açık / koyu tema"
        title="Açık / koyu tema"
        onClick={toggleTheme}
      >
        <svg className="icon-moon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
        </svg>
        <svg className="icon-sun" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
        </svg>
      </button>

      <div className="nav-links">
        {links.map((l) => (
          <Link
            key={l.href}
            href={l.href}
            className={`nav-link${path === l.href || path.startsWith(l.href + "/") ? " active" : ""}`}
          >
            {l.label}
          </Link>
        ))}
      </div>
    </nav>
  );
}
