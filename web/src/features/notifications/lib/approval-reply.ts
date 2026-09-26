export const APPROVAL_REPLY = {
  approve: "approved",
  reject: "rejected",
} as const;

export type ApprovalChoice = keyof typeof APPROVAL_REPLY;

export function approvalChoice(replyText: string | null | undefined): ApprovalChoice | null {
  if (replyText === APPROVAL_REPLY.approve) {
    return "approve";
  }
  if (replyText === APPROVAL_REPLY.reject) {
    return "reject";
  }
  return null;
}
