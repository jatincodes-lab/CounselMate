import React from "react";

export function Spinner({ className = "", ...props }) {
  return <span className={`ui-spinner ${className}`.trim()} aria-hidden="true" {...props} />;
}
