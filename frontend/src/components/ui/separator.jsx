import React from "react";

export function Separator({ className = "", orientation = "horizontal", ...props }) {
  return <div role="separator" aria-orientation={orientation} className={`ui-separator ui-separator--${orientation} ${className}`.trim()} {...props} />;
}
