import React from "react";

export function Badge({ className = "", variant = "default", ...props }) {
  return <span className={`ui-badge ui-badge--${variant} ${className}`.trim()} {...props} />;
}
