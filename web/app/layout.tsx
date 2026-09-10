import type { Metadata } from "next";
import { Newsreader, Inter } from "next/font/google";
import "./globals.css";
import { Nav } from "@/components/Nav";

const heading = Newsreader({
  subsets: ["latin"],
  weight: ["400", "500", "600"],
  variable: "--font-heading",
});

const body = Inter({
  subsets: ["latin", "latin-ext"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-body",
});

export const metadata: Metadata = {
  title: "Yazıcı Takip",
  description: "Ağ yazıcıları SNMP sayaç takibi",
};

const themeInit = `(function(){try{var t=localStorage.getItem('yt-theme');if(t!=='dark'&&t!=='light'){t=window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light';}document.documentElement.setAttribute('data-theme',t);}catch(e){}})();`;

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="tr" className={`${heading.variable} ${body.variable}`} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInit }} />
      </head>
      <body suppressHydrationWarning>
        <Nav />
        <main className="container">{children}</main>
        <footer className="footer">© {new Date().getFullYear()} Yazıcı Takip</footer>
      </body>
    </html>
  );
}
