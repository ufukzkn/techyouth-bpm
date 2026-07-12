"use client";

import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { LoginRedirectLoading, WorkspaceLoadingShell } from "@/features/app-shell/components/WorkspaceLoadingShell";
import { WorkspaceSessionController } from "@/features/app-shell/components/WorkspaceSessionController";
import { WorkspaceSidebar } from "@/features/app-shell/components/WorkspaceSidebar";
import { WorkspaceTopbar } from "@/features/app-shell/components/WorkspaceTopbar";
import { navItems } from "@/features/app-shell/navigation";
import { ForcedPasswordChangeView } from "@/features/app-shell/views/ForcedPasswordChangeView";
import { useSessionStore } from "@/features/session/sessionStore";
import { api } from "@/lib/api";

function canUseNavItem(userPermissions: string[], requiredPermissions?: string[]) {
  if (!requiredPermissions?.length) {
    return true;
  }

  return requiredPermissions.some((permission) => userPermissions.includes(permission));
}

export default function WorkspaceLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const {
    user,
    token,
    expiresAt,
    theme,
    language,
    hasHydrated,
    logout,
    setUser,
    toggleLanguage,
    toggleTheme,
  } = useSessionStore();
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const [isManagementOpen, setIsManagementOpen] = useState(() => pathname.startsWith("/management"));

  const visibleNavItems = useMemo(
    () =>
      navItems.filter((item) => {
        if (!user || !canUseNavItem(user.permissions ?? [], item.permissions)) {
          return false;
        }

        return user.mustChangePassword ? item.viewId === "settings" : true;
      }),
    [user],
  );

  const currentViewId = navItems.find((item) => item.path === pathname)?.viewId;
  const canAccessCurrentRoute = visibleNavItems.some((item) => item.viewId === currentViewId);
  const isManagementExpanded = pathname.startsWith("/management") || isManagementOpen;

  const endSession = useCallback(() => {
    logout();
    router.replace("/login");
  }, [logout, router]);

  const requestLogout = useCallback(() => {
    if (token && !token.startsWith("demo-")) {
      void api.logout(token).finally(endSession);
      return;
    }

    endSession();
  }, [endSession, token]);

  useEffect(() => {
    if (!isMobileNavOpen) {
      return;
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsMobileNavOpen(false);
      }
    }

    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [isMobileNavOpen]);

  useEffect(() => {
    if (!hasHydrated || !user || user.mustChangePassword || canAccessCurrentRoute) {
      return;
    }

    router.replace(visibleNavItems[0]?.path ?? "/dashboard");
  }, [canAccessCurrentRoute, hasHydrated, router, user, visibleNavItems]);

  useEffect(() => {
    if (hasHydrated && !user) {
      router.replace("/login");
    }
  }, [hasHydrated, router, user]);

  return (
    <>
      <WorkspaceSessionController />
      {!hasHydrated ? <WorkspaceLoadingShell /> : null}
      {hasHydrated && !user ? <LoginRedirectLoading /> : null}
      {hasHydrated && user?.mustChangePassword ? (
        <ForcedPasswordChangeView
          language={language}
          token={token}
          user={user}
          onLogout={endSession}
          onUserUpdated={setUser}
          onToggleLanguage={toggleLanguage}
          onToggleTheme={toggleTheme}
          theme={theme}
        />
      ) : null}
      {hasHydrated && user && !user.mustChangePassword && canAccessCurrentRoute ? (
        <div className="app-shell">
          <WorkspaceSidebar
            isManagementOpen={isManagementExpanded}
            isMobileOpen={isMobileNavOpen}
            items={visibleNavItems}
            language={language}
            pathname={pathname}
            onCloseMobile={() => setIsMobileNavOpen(false)}
            onToggleManagement={() => setIsManagementOpen((isOpen) => !isOpen)}
          />
          <div className="main-area">
            <WorkspaceTopbar
              expiresAt={expiresAt}
              isMobileNavOpen={isMobileNavOpen}
              language={language}
              theme={theme}
              token={token}
              user={user}
              onLogout={requestLogout}
              onToggleLanguage={toggleLanguage}
              onToggleMobileNav={() => setIsMobileNavOpen((isOpen) => !isOpen)}
              onToggleTheme={toggleTheme}
            />
            <main className="content">
              <div className="workspace-route-content" key={pathname}>{children}</div>
            </main>
          </div>
        </div>
      ) : null}
    </>
  );
}
