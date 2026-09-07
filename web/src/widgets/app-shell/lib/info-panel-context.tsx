"use client";

import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";

interface InfoPanelActions {
  setContent: (content: ReactNode) => void;
  clearContent: () => void;
}

const InfoPanelActionsContext = createContext<InfoPanelActions | null>(null);
const InfoPanelContentContext = createContext<ReactNode>(null);

interface InfoPanelProviderProps {
  children: ReactNode;
}

export function InfoPanelProvider({ children }: InfoPanelProviderProps) {
  const [content, setContent] = useState<ReactNode>(null);

  const set = useCallback((next: ReactNode) => {
    setContent(next);
  }, []);

  const clear = useCallback(() => {
    setContent(null);
  }, []);

  return (
    <InfoPanelActionsContext.Provider value={{ setContent: set, clearContent: clear }}>
      <InfoPanelContentContext.Provider value={content}>
        {children}
      </InfoPanelContentContext.Provider>
    </InfoPanelActionsContext.Provider>
  );
}

export function useInfoPanel(content: ReactNode) {
  const actions = useContext(InfoPanelActionsContext);
  if (!actions) {
    throw new Error("useInfoPanel must be used within InfoPanelProvider");
  }

  useEffect(() => {
    actions.setContent(content);
    return () => actions.clearContent();
  }, [actions, content]);
}

export function useInfoPanelContent(): ReactNode {
  return useContext(InfoPanelContentContext);
}
