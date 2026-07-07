import React from "react";

export function Alert({ className = "", variant = "default", title, children, ...props }) {
  return (
    <div className={`ui-alert ui-alert--${variant} ${className}`.trim()} {...props}>
      <div className="ui-alert-copy">
        {title && <strong>{title}</strong>}
        {children && <div>{children}</div>}
      </div>
    </div>
  );
}
