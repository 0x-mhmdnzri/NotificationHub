import type { Metadata } from "next";
import { Toaster } from "sonner";
import { AppSidebar } from "@/components/app-sidebar";
import "./globals.css";

export const metadata: Metadata = {
  title: "NotificationHub Admin",
  description: "Production-ready demo console for NotificationHub",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="dark">
      <body>
        <div className="flex min-h-screen">
          <AppSidebar />
          <main className="flex-1 overflow-auto">
            <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">{children}</div>
          </main>
        </div>
        <Toaster theme="dark" richColors position="top-right" closeButton />
      </body>
    </html>
  );
}
