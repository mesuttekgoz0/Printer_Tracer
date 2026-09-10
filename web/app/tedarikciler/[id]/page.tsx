import { TedarikciDetailClient } from "@/components/TedarikciDetailClient";

export const metadata = { title: "Tedarikçi · Yazıcı Takip" };

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <TedarikciDetailClient id={Number(id)} />;
}
