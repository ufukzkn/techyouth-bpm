"use client";

import { Blocks, X, type LucideIcon } from "lucide-react";
import { useEffect, useMemo, useRef, useState, useSyncExternalStore, type CSSProperties, type PointerEvent as ReactPointerEvent } from "react";
import { createPortal } from "react-dom";
import type { FieldType } from "@/lib/types";

const storageKey = "techyouth-form-palette-fab-position";
const buttonSize = 54;
const safeInset = 14;
const safeTop = 78;
const dragThreshold = 7;
const defaultPosition: StoredPosition = { edge: "right", yRatio: 0.82 };
const subscribeToHydration = () => () => undefined;

type StoredPosition = {
  edge: "left" | "right";
  yRatio: number;
};

type Point = { x: number; y: number };

type MobileFieldPaletteProps = {
  closeLabel: string;
  description: string;
  items: Array<{
    description: string;
    icon: LucideIcon;
    label: string;
    type: FieldType;
  }>;
  onSelect: (fieldType: FieldType) => void;
  openLabel: string;
  title: string;
};

export function MobileFieldPalette({
  closeLabel,
  description,
  items,
  onSelect,
  openLabel,
  title,
}: MobileFieldPaletteProps) {
  const mounted = useSyncExternalStore(subscribeToHydration, () => true, () => false);
  const [isOpen, setIsOpen] = useState(false);
  const [position, setPosition] = useState<StoredPosition>(() =>
    typeof window === "undefined" ? defaultPosition : readStoredPosition() ?? defaultPosition,
  );
  const [draftPoint, setDraftPoint] = useState<Point | null>(null);
  const [viewport, setViewport] = useState(() =>
    typeof window === "undefined" ? { width: 390, height: 844 } : { width: window.innerWidth, height: window.innerHeight },
  );
  const [sheetDragY, setSheetDragY] = useState(0);
  const bubbleRef = useRef<HTMLButtonElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const sheetRef = useRef<HTMLElement>(null);
  const bubbleDragRef = useRef<{ pointerId: number; start: Point; origin: Point } | null>(null);
  const sheetDragRef = useRef<{ pointerId: number; startY: number } | null>(null);

  useEffect(() => {
    function updateViewport() {
      setViewport({ width: window.innerWidth, height: window.innerHeight });
      if (window.innerWidth > 860) {
        setIsOpen(false);
      }
    }

    window.addEventListener("resize", updateViewport);
    return () => window.removeEventListener("resize", updateViewport);
  }, []);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    const bubble = bubbleRef.current;
    document.body.style.overflow = "hidden";
    closeRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
      bubble?.focus();
    };
  }, [isOpen]);

  const restingPoint = useMemo(() => positionToPoint(position, viewport), [position, viewport]);
  const bubblePoint = draftPoint ?? restingPoint;
  const bubbleStyle: CSSProperties = { left: bubblePoint.x, top: bubblePoint.y };

  function handleBubblePointerDown(event: ReactPointerEvent<HTMLButtonElement>) {
    if (event.pointerType === "mouse" && event.button !== 0) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    bubbleDragRef.current = {
      pointerId: event.pointerId,
      start: { x: event.clientX, y: event.clientY },
      origin: bubblePoint,
    };
  }

  function handleBubblePointerMove(event: ReactPointerEvent<HTMLButtonElement>) {
    const drag = bubbleDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    setDraftPoint(clampPoint({
      x: drag.origin.x + event.clientX - drag.start.x,
      y: drag.origin.y + event.clientY - drag.start.y,
    }, viewport));
  }

  function handleBubblePointerUp(event: ReactPointerEvent<HTMLButtonElement>) {
    const drag = bubbleDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    const distance = Math.hypot(event.clientX - drag.start.x, event.clientY - drag.start.y);
    bubbleDragRef.current = null;
    if (distance < dragThreshold) {
      setDraftPoint(null);
      setIsOpen(true);
      return;
    }

    const point = clampPoint({
      x: drag.origin.x + event.clientX - drag.start.x,
      y: drag.origin.y + event.clientY - drag.start.y,
    }, viewport);
    const nextPosition = pointToStoredPosition(point, viewport);
    setPosition(nextPosition);
    setDraftPoint(null);
    window.localStorage.setItem(storageKey, JSON.stringify(nextPosition));
  }

  function handleSheetPointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    event.currentTarget.setPointerCapture(event.pointerId);
    sheetDragRef.current = { pointerId: event.pointerId, startY: event.clientY };
  }

  function handleSheetPointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    const drag = sheetDragRef.current;
    if (drag?.pointerId === event.pointerId) {
      setSheetDragY(Math.max(0, event.clientY - drag.startY));
    }
  }

  function handleSheetPointerUp(event: ReactPointerEvent<HTMLDivElement>) {
    const drag = sheetDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    sheetDragRef.current = null;
    if (sheetDragY > 80) {
      setIsOpen(false);
    }
    setSheetDragY(0);
  }

  if (!mounted) {
    return null;
  }

  return createPortal(
    <>
      <button
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        aria-label={openLabel}
        className="mobile-field-palette-fab"
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            setIsOpen(true);
          }
        }}
        onPointerDown={handleBubblePointerDown}
        onPointerMove={handleBubblePointerMove}
        onPointerUp={handleBubblePointerUp}
        ref={bubbleRef}
        style={bubbleStyle}
        title={openLabel}
        type="button"
      >
        <Blocks size={22} aria-hidden="true" />
      </button>

      {isOpen ? (
        <div className="mobile-field-palette-layer">
          <button className="mobile-field-palette-backdrop" aria-label={closeLabel} onClick={() => setIsOpen(false)} type="button" />
          <section
            aria-labelledby="mobile-field-palette-title"
            aria-modal="true"
            className="mobile-field-palette-sheet"
            onKeyDown={(event) => {
              if (event.key !== "Tab") {
                return;
              }
              const focusable = sheetRef.current?.querySelectorAll<HTMLElement>("button:not([disabled])");
              if (!focusable?.length) {
                return;
              }
              const first = focusable[0];
              const last = focusable[focusable.length - 1];
              if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
              } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
              }
            }}
            ref={sheetRef}
            role="dialog"
            style={{ transform: `translateY(${sheetDragY}px)` }}
          >
            <div
              className="mobile-field-palette-handle"
              onPointerDown={handleSheetPointerDown}
              onPointerMove={handleSheetPointerMove}
              onPointerUp={handleSheetPointerUp}
            >
              <span aria-hidden="true" />
            </div>
            <div className="mobile-field-palette-heading">
              <div>
                <h2 id="mobile-field-palette-title">{title}</h2>
                <p>{description}</p>
              </div>
              <button className="icon-button" aria-label={closeLabel} onClick={() => setIsOpen(false)} ref={closeRef} type="button">
                <X size={18} />
              </button>
            </div>
            <div className="mobile-field-palette-options">
              {items.map((item) => (
                <button
                  className="mobile-field-palette-option"
                  key={item.type}
                  onClick={() => {
                    onSelect(item.type);
                    setIsOpen(false);
                  }}
                  type="button"
                >
                  <item.icon size={20} aria-hidden="true" />
                  <span>
                    <strong>{item.label}</strong>
                    <small>{item.description}</small>
                  </span>
                </button>
              ))}
            </div>
          </section>
        </div>
      ) : null}
    </>,
    document.body,
  );
}

function clampPoint(point: Point, viewport: { width: number; height: number }): Point {
  return {
    x: Math.min(Math.max(point.x, safeInset), Math.max(safeInset, viewport.width - buttonSize - safeInset)),
    y: Math.min(Math.max(point.y, safeTop), Math.max(safeTop, viewport.height - buttonSize - safeInset)),
  };
}

function positionToPoint(position: StoredPosition, viewport: { width: number; height: number }): Point {
  const availableHeight = Math.max(1, viewport.height - safeTop - buttonSize - safeInset);
  return clampPoint({
    x: position.edge === "left" ? safeInset : viewport.width - buttonSize - safeInset,
    y: safeTop + availableHeight * Math.min(Math.max(position.yRatio, 0), 1),
  }, viewport);
}

function pointToStoredPosition(point: Point, viewport: { width: number; height: number }): StoredPosition {
  const availableHeight = Math.max(1, viewport.height - safeTop - buttonSize - safeInset);
  return {
    edge: point.x + buttonSize / 2 < viewport.width / 2 ? "left" : "right",
    yRatio: Math.min(Math.max((point.y - safeTop) / availableHeight, 0), 1),
  };
}

function readStoredPosition(): StoredPosition | null {
  try {
    const value = JSON.parse(window.localStorage.getItem(storageKey) ?? "null") as Partial<StoredPosition> | null;
    return value && (value.edge === "left" || value.edge === "right") && typeof value.yRatio === "number"
      ? { edge: value.edge, yRatio: value.yRatio }
      : null;
  } catch {
    return null;
  }
}
