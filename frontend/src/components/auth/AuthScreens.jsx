import { ArrowLeft, Check, Eye, EyeOff, LockKeyhole, Mail, ShieldCheck } from "lucide-react";
import React, { useState } from "react";
import counselMateLogo from "../../assets/counselmate-logo.png";
import {
  Alert,
  Badge,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  Checkbox,
  Input,
  Label,
  Spinner,
} from "../ui";

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function firstError(value) {
  return Array.isArray(value) ? value[0] || "" : value || "";
}

function AuthLayout({ eyebrow, title, description, children }) {
  return (
    <main className="auth-shell">
      <section className="auth-layout" aria-label="CounselMate account access">
        <aside className="auth-brand-panel">
          <div className="auth-brand-glow" aria-hidden="true" />
          <img className="auth-brand-logo" src={counselMateLogo} alt="CounselMate CRM" />

          <div className="auth-brand-copy">
            <Badge className="auth-brand-badge" variant="outline">Admissions CRM</Badge>
            <h1>Turn every enquiry into a guided student journey.</h1>
            <p>One secure workspace for leads, follow-ups, applications, enrollments, and team performance.</p>

            <ul className="auth-benefits" aria-label="CounselMate benefits">
              <li><Check size={16} aria-hidden="true" /> Keep every conversation and next action visible</li>
              <li><Check size={16} aria-hidden="true" /> Coordinate counsellors with clear ownership</li>
              <li><Check size={16} aria-hidden="true" /> Track progress from enquiry to enrollment</li>
            </ul>
          </div>

          <div className="auth-security-note">
            <ShieldCheck size={18} aria-hidden="true" />
            <span>Secure access for your institute workspace</span>
          </div>
        </aside>

        <div className="auth-form-zone">
          <div className="auth-mobile-brand">
            <img src={counselMateLogo} alt="CounselMate CRM" />
          </div>
          <Card className="auth-card">
            <CardHeader className="auth-card-header">
              <div className="auth-form-icon" aria-hidden="true"><LockKeyhole size={20} /></div>
              <div>
                <span className="auth-eyebrow">{eyebrow}</span>
                <h1 className="ui-card-title">{title}</h1>
                <CardDescription>{description}</CardDescription>
              </div>
            </CardHeader>
            <CardContent className="auth-card-content">{children}</CardContent>
          </Card>
          <p className="auth-footer-copy">© {new Date().getFullYear()} CounselMate. Institute access only.</p>
        </div>
      </section>
    </main>
  );
}

function AuthField({ id, label, error, children }) {
  const errorId = `${id}-error`;
  return (
    <div className="auth-field">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error && <p id={errorId} className="auth-field-error" role="alert">{error}</p>}
    </div>
  );
}

export function LoginScreen({ error, signingIn, onSubmit, onForgot }) {
  const [form, setForm] = useState({ email: "", password: "", remember: true });
  const [fieldErrors, setFieldErrors] = useState({});
  const [passwordVisible, setPasswordVisible] = useState(false);

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const email = form.email.trim();
    const nextErrors = {};
    if (!email) nextErrors.email = "Enter your email address.";
    else if (!emailPattern.test(email)) nextErrors.email = "Enter a valid email address.";
    if (!form.password) nextErrors.password = "Enter your password.";

    setFieldErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;
    onSubmit({ email, password: form.password, remember: form.remember });
  };

  return (
    <AuthLayout eyebrow="Welcome back" title="Sign in to CounselMate" description="Enter your credentials to continue to your CRM workspace.">
      <form className="auth-form" onSubmit={handleSubmit} noValidate>
        {error && <Alert variant="destructive" role="alert" title="Unable to sign in">{error}</Alert>}

        <fieldset className="auth-fieldset" disabled={signingIn}>
          <AuthField id="login-email" label="Email address" error={fieldErrors.email}>
            <div className={`auth-input-wrap ${fieldErrors.email ? "has-error" : ""}`}>
              <Mail size={17} aria-hidden="true" />
              <Input
                id="login-email"
                name="email"
                value={form.email}
                type="email"
                inputMode="email"
                autoComplete="username"
                maxLength={240}
                onChange={(event) => updateField("email", event.target.value)}
                placeholder="you@institute.edu"
                aria-invalid={Boolean(fieldErrors.email)}
                aria-describedby={fieldErrors.email ? "login-email-error" : undefined}
                autoFocus
              />
            </div>
          </AuthField>

          <AuthField id="login-password" label="Password" error={fieldErrors.password}>
            <div className={`auth-input-wrap auth-password-wrap ${fieldErrors.password ? "has-error" : ""}`}>
              <LockKeyhole size={17} aria-hidden="true" />
              <Input
                id="login-password"
                name="password"
                value={form.password}
                type={passwordVisible ? "text" : "password"}
                autoComplete="current-password"
                maxLength={120}
                onChange={(event) => updateField("password", event.target.value)}
                placeholder="Enter your password"
                aria-invalid={Boolean(fieldErrors.password)}
                aria-describedby={fieldErrors.password ? "login-password-error" : undefined}
              />
              <Button
                className="auth-password-toggle"
                variant="ghost"
                size="icon"
                type="button"
                onClick={() => setPasswordVisible((current) => !current)}
                aria-label={passwordVisible ? "Hide password" : "Show password"}
                aria-pressed={passwordVisible}
              >
                {passwordVisible ? <EyeOff size={18} /> : <Eye size={18} />}
              </Button>
            </div>
          </AuthField>

          <div className="auth-options">
            <Label className="auth-remember" htmlFor="remember-session">
              <Checkbox
                id="remember-session"
                checked={form.remember}
                onChange={(event) => updateField("remember", event.target.checked)}
              />
              <span>Keep me signed in</span>
            </Label>
            <Button className="auth-text-button" variant="ghost" size="sm" type="button" onClick={onForgot}>
              Forgot password?
            </Button>
          </div>

          <Button className="auth-submit" type="submit" disabled={signingIn}>
            {signingIn && <Spinner />}
            {signingIn ? "Signing in…" : "Sign in"}
          </Button>
        </fieldset>
      </form>
    </AuthLayout>
  );
}

export function ForgotPasswordScreen({ status, onSubmit, onBack }) {
  const [email, setEmail] = useState("");
  const [clientError, setClientError] = useState("");
  const serverEmailError = firstError(status.fieldErrors?.email);

  const handleSubmit = (event) => {
    event.preventDefault();
    const normalizedEmail = email.trim();
    if (!normalizedEmail) {
      setClientError("Enter your email address.");
      return;
    }
    if (!emailPattern.test(normalizedEmail)) {
      setClientError("Enter a valid email address.");
      return;
    }
    setClientError("");
    onSubmit({ email: normalizedEmail });
  };

  const emailError = clientError || serverEmailError;
  return (
    <AuthLayout eyebrow="Account recovery" title="Reset your password" description="We’ll help you regain access to your institute workspace.">
      <form className="auth-form" onSubmit={handleSubmit} noValidate>
        {status.error && <Alert variant="destructive" role="alert" title="Request failed">{status.error}</Alert>}
        {status.message && <Alert variant="success" role="status" title="Request received">{status.message}</Alert>}

        <fieldset className="auth-fieldset" disabled={status.submitting}>
          <AuthField id="reset-email" label="Email address" error={emailError}>
            <div className={`auth-input-wrap ${emailError ? "has-error" : ""}`}>
              <Mail size={17} aria-hidden="true" />
              <Input
                id="reset-email"
                name="email"
                value={email}
                type="email"
                inputMode="email"
                autoComplete="email"
                maxLength={240}
                onChange={(event) => { setEmail(event.target.value); setClientError(""); }}
                placeholder="you@institute.edu"
                aria-invalid={Boolean(emailError)}
                aria-describedby={emailError ? "reset-email-error" : undefined}
                autoFocus
              />
            </div>
          </AuthField>

          <Button className="auth-submit" type="submit" disabled={status.submitting}>
            {status.submitting && <Spinner />}
            {status.submitting ? "Sending request…" : "Send reset instructions"}
          </Button>

          <Button className="auth-back-button" variant="ghost" type="button" onClick={onBack}>
            <ArrowLeft size={16} aria-hidden="true" /> Back to sign in
          </Button>
        </fieldset>
      </form>
    </AuthLayout>
  );
}
