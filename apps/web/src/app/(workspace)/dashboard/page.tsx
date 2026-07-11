"use client";

import { useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import { navItems, type ViewId } from "@/features/app-shell/navigation";
import { DashboardView } from "@/features/app-shell/views/DashboardView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function DashboardPage() {
  const { user, token, language } = useSessionStore();
  const router = useRouter();

  const navigate = useCallback(
    (viewId: ViewId) => {
      const item = navItems.find((navItem) => navItem.viewId === viewId);
      router.push(item?.path ?? "/dashboard");
    },
    [router],
  );

  const visibleViewIds = useMemo(
    () =>
      navItems
        .filter((item) => {
          if (!user) return false;
          if (!item.permissions?.length) return true;
          return item.permissions.some((p) => (user.permissions ?? []).includes(p));
        })
        .map((item) => item.viewId),
    [user],
  );

  if (!user) return null;

  return (
    <DashboardView
      token={token}
      user={user}
      language={language}
      visibleViewIds={visibleViewIds}
      onNavigate={navigate}
    />
  );
}
