import { HakedisDetailClient } from "@/components/HakedisDetailClient";

export const metadata = { title: "Hakediş · Yazıcı Takip" };

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <HakedisDetailClient id={Number(id)} />;
}
