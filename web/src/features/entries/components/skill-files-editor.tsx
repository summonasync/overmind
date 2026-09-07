"use client";

import { useState } from "react";
import { AGENT_PLATFORMS, type AgentPlatform } from "@/shared/api/types";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Textarea } from "@/shared/ui/textarea";
import { cn } from "@/shared/lib/cn";
import type { SkillFileDto } from "@/features/entries/types";

interface SkillFilesEditorProps {
  files: SkillFileDto[];
  onChange: (files: SkillFileDto[]) => void;
}

export function SkillFilesEditor({ files, onChange }: SkillFilesEditorProps) {
  const [activePlatform, setActivePlatform] = useState<AgentPlatform>("claude");
  const [newPath, setNewPath] = useState("");

  const platformFiles = files.filter((file) => file.platform === activePlatform);

  const updateFile = (relativePath: string, content: string) => {
    onChange(
      files.map((file) =>
        file.platform === activePlatform && file.relativePath === relativePath
          ? { ...file, content }
          : file,
      ),
    );
  };

  const removeFile = (relativePath: string) => {
    onChange(
      files.filter(
        (file) => !(file.platform === activePlatform && file.relativePath === relativePath),
      ),
    );
  };

  const addFile = () => {
    const relativePath = newPath.trim();
    if (!relativePath) {
      return;
    }
    if (platformFiles.some((file) => file.relativePath === relativePath)) {
      return;
    }
    onChange([...files, { platform: activePlatform, relativePath, content: "" }]);
    setNewPath("");
  };

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-1">
        {AGENT_PLATFORMS.map((platform) => (
          <button
            key={platform}
            type="button"
            onClick={() => setActivePlatform(platform)}
            className={cn(
              "rounded-md px-2 py-1 text-xs font-medium capitalize focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
              platform === activePlatform
                ? "bg-primary/15 text-primary"
                : "bg-surface-raised text-muted hover:text-foreground",
            )}
          >
            {platform}
            <Badge tone="neutral" className="ml-1">
              {files.filter((file) => file.platform === platform).length}
            </Badge>
          </button>
        ))}
      </div>

      {platformFiles.length === 0 ? (
        <p className="text-xs text-muted">No files for this platform yet.</p>
      ) : (
        <ul className="space-y-2">
          {platformFiles.map((file) => (
            <li key={file.relativePath} className="rounded-md border border-border bg-surface p-2">
              <div className="mb-1 flex items-center justify-between">
                <span className="font-mono text-xs text-foreground">{file.relativePath}</span>
                <Button variant="ghost" className="px-2 py-0.5 text-xs" onClick={() => removeFile(file.relativePath)}>
                  Remove
                </Button>
              </div>
              <Textarea
                rows={6}
                value={file.content}
                onChange={(event) => updateFile(file.relativePath, event.target.value)}
                placeholder="File content"
              />
            </li>
          ))}
        </ul>
      )}

      <div className="flex gap-2">
        <Input
          value={newPath}
          onChange={(event) => setNewPath(event.target.value)}
          placeholder=".kimi-code/skills/my-skill/SKILL.md"
          className="font-mono text-xs"
        />
        <Button variant="secondary" onClick={addFile}>
          Add file
        </Button>
      </div>
    </div>
  );
}
