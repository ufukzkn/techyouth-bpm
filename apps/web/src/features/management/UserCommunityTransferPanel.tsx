"use client";

import { ArrowRight, ArrowRightLeft, BriefcaseBusiness } from "lucide-react";
import { useEffect, useState } from "react";
import { ActionFeedback, InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import { DisclosureSection } from "@/features/app-shell/components/DisclosureSection";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { api } from "@/lib/api";
import type {
  Community,
  CommunityRole,
  CommunityTransferPreview,
  Language,
  UserAdmin,
} from "@/lib/types";

type Feedback = { tone: "success" | "error" | "loading"; text: string } | null;

export function UserCommunityTransferPanel({
  communities,
  language,
  onTransferred,
  selectedUser,
  token,
}: {
  communities: Community[];
  language: Language;
  onTransferred: () => Promise<void> | void;
  selectedUser: UserAdmin;
  token: string | null;
}) {
  const isTr = language === "tr";
  const [isOpen, setIsOpen] = useState(false);
  const [targetCommunityId, setTargetCommunityId] = useState("");
  const [targetRoleId, setTargetRoleId] = useState("");
  const [roles, setRoles] = useState<CommunityRole[]>([]);
  const [isLoadingRoles, setIsLoadingRoles] = useState(false);
  const [preview, setPreview] = useState<CommunityTransferPreview | null>(null);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const [isTransferring, setIsTransferring] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);

  useEffect(() => {
    let ignore = false;

    if (!token || !targetCommunityId) {
      return;
    }

    async function loadRoles() {
      setIsLoadingRoles(true);
      try {
        const result = await api.listCommunityRoles(token!, targetCommunityId);
        if (!ignore) setRoles(result);
      } catch (error) {
        if (!ignore) {
          setRoles([]);
          setFeedback({
            tone: "error",
            text: localizeApiError(
              error,
              language,
              isTr ? "Hedef topluluğun rolleri yüklenemedi." : "Target community roles could not be loaded.",
            ),
          });
        }
      } finally {
        if (!ignore) setIsLoadingRoles(false);
      }
    }

    void loadRoles();

    return () => {
      ignore = true;
    };
  }, [isTr, language, targetCommunityId, token]);

  async function loadPreview() {
    if (!token || !targetCommunityId || !targetRoleId) return;
    setIsPreviewing(true);
    setFeedback({ tone: "loading", text: isTr ? "Taşıma önizlemesi hazırlanıyor..." : "Preparing transfer preview..." });
    try {
      const result = await api.previewUserCommunityTransfer(token, selectedUser.id, {
        targetCommunityId,
        targetCommunityRoleId: targetRoleId,
      });
      setPreview(result);
      setFeedback(
        result.canTransfer
          ? { tone: "success", text: isTr ? "Taşıma için engel bulunmadı." : "No blocker was found for this transfer." }
          : { tone: "error", text: isTr ? "Kullanıcının önce tamamlanması veya bırakılması gereken işleri var." : "The user has tasks that must be completed or released first." },
      );
    } catch (error) {
      setPreview(null);
      setFeedback({
        tone: "error",
        text: localizeApiError(error, language, isTr ? "Taşıma önizlemesi alınamadı." : "Transfer preview could not be loaded."),
      });
    } finally {
      setIsPreviewing(false);
    }
  }

  async function transfer() {
    if (!token || !preview?.canTransfer) return;
    setIsConfirming(false);
    setIsTransferring(true);
    setFeedback({ tone: "loading", text: isTr ? "Kullanıcı atomik olarak taşınıyor..." : "Transferring user atomically..." });
    try {
      await api.transferUserCommunity(token, selectedUser.id, {
        targetCommunityId: preview.targetCommunityId,
        targetCommunityRoleId: preview.targetCommunityRoleId,
      });
      setFeedback({
        tone: "success",
        text: isTr
          ? `${selectedUser.displayName}, ${preview.targetCommunityName} topluluğuna taşındı.`
          : `${selectedUser.displayName} was transferred to ${preview.targetCommunityName}.`,
      });
      setPreview(null);
      setTargetCommunityId("");
      setTargetRoleId("");
      await onTransferred();
    } catch (error) {
      setFeedback({
        tone: "error",
        text: localizeApiError(error, language, isTr ? "Kullanıcı taşınamadı." : "User could not be transferred."),
      });
    } finally {
      setIsTransferring(false);
    }
  }

  const availableCommunities = communities.filter(
    (community) => community.isActive && community.id !== selectedUser.communityId,
  );

  return (
    <>
      <DisclosureSection
        className="user-community-transfer-panel"
        description={
          isTr
            ? "Topluluk, rol, takım üyelikleri ve oturumlar tek işlemde güvenli biçimde güncellenir."
            : "Community, role, team memberships and sessions are updated safely in one operation."
        }
        eyebrow={isTr ? "Platform yönetimi" : "Platform management"}
        icon={<ArrowRightLeft size={18} />}
        isOpen={isOpen}
        onToggle={() => setIsOpen((value) => !value)}
        title={isTr ? "Kullanıcıyı taşı" : "Transfer user"}
      >
        <div className="community-transfer-form">
          <label>
            <span>{isTr ? "Hedef topluluk" : "Target community"}</span>
            <select
              disabled={isTransferring}
              onChange={(event) => {
                setTargetRoleId("");
                setRoles([]);
                setPreview(null);
                setTargetCommunityId(event.target.value);
                setFeedback(null);
              }}
              value={targetCommunityId}
            >
              <option value="">{isTr ? "Topluluk seçin" : "Select community"}</option>
              {availableCommunities.map((community) => (
                <option key={community.id} value={community.id}>{community.name}</option>
              ))}
            </select>
          </label>
          <label>
            <span>{isTr ? "Hedef rol" : "Target role"}</span>
            <select
              disabled={!targetCommunityId || isLoadingRoles || isTransferring}
              onChange={(event) => {
                setTargetRoleId(event.target.value);
                setPreview(null);
                setFeedback(null);
              }}
              value={targetRoleId}
            >
              <option value="">{isLoadingRoles ? (isTr ? "Roller yükleniyor" : "Loading roles") : (isTr ? "Rol seçin" : "Select role")}</option>
              {roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
            </select>
          </label>
          <button
            className="secondary-button"
            disabled={!targetRoleId || isPreviewing || isTransferring}
            onClick={() => void loadPreview()}
            type="button"
          >
            {isPreviewing ? <InlineValueLoader label={isTr ? "Önizleniyor" : "Previewing"} /> : <ArrowRight size={16} />}
            {isTr ? "Taşımayı önizle" : "Preview transfer"}
          </button>
        </div>

        {preview ? (
          <div className="community-transfer-preview">
            <div>
              <span>{isTr ? "Mevcut" : "Current"}</span>
              <strong>{preview.currentCommunityName || "-"}</strong>
              <small>{preview.currentCommunityRoleName || (isTr ? "Atanmadı" : "Unassigned")}</small>
            </div>
            <ArrowRight size={20} aria-hidden="true" />
            <div>
              <span>{isTr ? "Hedef" : "Target"}</span>
              <strong>{preview.targetCommunityName}</strong>
              <small>{preview.targetCommunityRoleName}</small>
            </div>
          </div>
        ) : null}

        {preview?.blockingTasks.length ? (
          <div className="community-transfer-blockers">
            <strong>{isTr ? "Taşımayı engelleyen işler" : "Tasks blocking transfer"}</strong>
            {preview.blockingTasks.map((task) => (
              <article key={task.id}>
                <BriefcaseBusiness size={16} />
                <span><strong>{task.title}</strong><small>{task.workflowName}</small></span>
              </article>
            ))}
          </div>
        ) : null}

        <div className="section-actions">
          <ActionFeedback feedback={feedback} />
          {preview?.canTransfer ? (
            <button className="primary-button" disabled={isTransferring} onClick={() => setIsConfirming(true)} type="button">
              <ArrowRightLeft size={16} /> {isTr ? "Taşımayı onayla" : "Confirm transfer"}
            </button>
          ) : null}
        </div>
      </DisclosureSection>

      {isConfirming && preview ? (
        <ConfirmationDialog
          confirmLabel={isTr ? "Kullanıcıyı taşı" : "Transfer user"}
          description={
            isTr
              ? `${preview.currentCommunityName} / ${preview.currentCommunityRoleName} üyeliği kapatılacak; ${preview.targetCommunityName} / ${preview.targetCommunityRoleName} üyeliği açılacak. Tüm aktif oturumlar sonlandırılacak.`
              : `${preview.currentCommunityName} / ${preview.currentCommunityRoleName} will be closed; ${preview.targetCommunityName} / ${preview.targetCommunityRoleName} will be activated. All active sessions will be revoked.`
          }
          eyebrow={isTr ? "Topluluk transferi" : "Community transfer"}
          onCancel={() => setIsConfirming(false)}
          onConfirm={() => void transfer()}
          title={isTr ? `${selectedUser.displayName} taşınsın mı?` : `Transfer ${selectedUser.displayName}?`}
          tone="primary"
        />
      ) : null}
    </>
  );
}
