import { ChevronDown } from "lucide-react";
import { useState } from "react";
import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import type { CommunityRole, CommunitySummary, PermissionName } from "@/lib/types";

export type CommunityPendingAction =
  | { type: "create-community" }
  | { type: "update-community" }
  | { type: "regenerate-code" }
  | { type: "create-role" }
  | { type: "update-role"; roleId: string }
  | { type: "delete-role"; roleId: string };

export function CommunityCardSkeleton() {
  return (
    <div aria-label="Topluluk bilgileri yukleniyor" className="community-card-skeleton">
      {Array.from({ length: 4 }, (_, index) => <SkeletonBlock className="skeleton-input" key={index} />)}
    </div>
  );
}

export function CommunityRolePanelSkeleton() {
  return (
    <div aria-label="Topluluk rolleri yukleniyor" className="community-role-skeleton">
      <SkeletonBlock className="skeleton-input" />
      <SkeletonBlock className="skeleton-input" />
      <SkeletonBlock className="skeleton-chip-row" />
      <SkeletonBlock className="skeleton-chip-row" />
    </div>
  );
}

export function RoleCountDisclosure({ summary }: { summary: CommunitySummary }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="role-count-disclosure">
      <button className="text-button" onClick={() => setOpen((value) => !value)} type="button">
        Rol dagilimini gor
        <ChevronDown className={open ? "nav-group-chevron open" : "nav-group-chevron"} size={15} />
      </button>
      {open ? (
        <div className="role-count-list">
          {summary.roleCounts.map((role) => <span key={role.communityRoleId}>{role.communityRoleName}<strong>{role.userCount}</strong></span>)}
        </div>
      ) : null}
    </div>
  );
}

export function ManagementConfirmation({
  action,
  isDeactivating,
  onCancel,
  onConfirm,
  replacementRoleId,
  roles,
  selectedRole,
  setReplacementRoleId,
}: {
  action: CommunityPendingAction;
  isDeactivating: boolean;
  onCancel: () => void;
  onConfirm: () => void;
  replacementRoleId: string;
  roles: CommunityRole[];
  selectedRole: CommunityRole | null;
  setReplacementRoleId: (value: string) => void;
}) {
  if (action.type === "delete-role") {
    return (
      <ConfirmationDialog
        confirmLabel="Tasi ve sil"
        description="Bu roldeki aktif kullanicilar secilen hedef role tasinir; islem geri alinmaz."
        eyebrow="Rol silme"
        onCancel={onCancel}
        onConfirm={onConfirm}
        title={`${selectedRole?.name ?? "Rol"} silinsin mi?`}
      >
        <label className="compact-form">
          <span>Hedef rol</span>
          <select onChange={(event) => setReplacementRoleId(event.target.value)} value={replacementRoleId}>
            {roles.filter((role) => role.id !== selectedRole?.id).map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
          </select>
        </label>
      </ConfirmationDialog>
    );
  }

  const isDeactivate = action.type === "update-community" && isDeactivating;
  const copy = action.type === "create-community"
    ? ["Topluluk olustur", "Yeni topluluk ve varsayilan sistem rolleri olusturulacak.", "Olustur"]
    : action.type === "regenerate-code"
      ? ["Davet kodu", "Eski kayit kodu gecersiz olur; yeni kodla kayit alinabilir.", "Kodu yenile"]
      : action.type === "create-role"
        ? ["Rol olustur", "Rol izinleri bu topluluk kapsaminda kullanilabilir olacak.", "Rolu olustur"]
        : action.type === "update-role"
          ? ["Rol guncelle", "Rol izinleri ve adi guncellenecek.", "Guncelle"]
          : [
              isDeactivate ? "Toplulugu pasife al" : "Toplulugu aktif et",
              isDeactivate
                ? "Normal uyelerin oturumlari kapatilacak; giris ve yeni workflow islemleri engellenecek."
                : "Topluluk uyeleri yeniden giris yaparak calisma alanina devam edebilecek.",
              isDeactivate ? "Pasife al" : "Aktif et",
            ];

  return (
    <ConfirmationDialog
      confirmLabel={copy[2]}
      description={copy[1]}
      eyebrow={copy[0]}
      onCancel={onCancel}
      onConfirm={onConfirm}
      title={`${copy[0]}?`}
      tone={isDeactivate ? "danger" : "primary"}
    />
  );
}

export function permissionLabel(permission: PermissionName) {
  return {
    "Community.ManageUsers": "Kullanicilari yonetir",
    "Community.ManageRoles": "Rolleri yonetir",
    "Community.ManageAdmins": "Topluluk adminlerini yonetir",
    "Forms.View": "Formlari gorur",
    "Forms.Create": "Form olusturur",
    "Forms.Update": "Form gunceller",
    "Processes.View": "Surecleri gorur",
    "Processes.Start": "Surec baslatir",
    "Tasks.View": "Isleri gorur",
    "Tasks.Act": "Is aksiyonu alir",
    "Audit.View": "Gecmisi gorur",
  }[permission];
}
