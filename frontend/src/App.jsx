import {
  ArrowDown,
  ArrowUp,
  BarChart3,
  Bell,
  BookOpen,
  Building2,
  CalendarDays,
  CheckCircle2,
  Download,
  Eye,
  EyeOff,
  FileText,
  GitBranch,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  MoreVertical,
  Pencil,
  Plus,
  RotateCcw,
  Radio,
  Search,
  Settings,
  UserCheck,
  UserPlus,
  UserX,
  Users,
  X,
} from "lucide-react";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
  addLeadActivity,
  archiveLead,
  assignLead,
  cancelLeadFollowUp,
  changePassword,
  completeLeadFollowUp,
  createLead,
  createLeadFollowUp,
  forgotPassword,
  getCrmData,
  getCurrentUser,
  getLeadDetail,
  getLeads,
  getPlatformTenants,
  getStoredAuth,
  getUsers,
  login,
  logout,
  createUser,
  createPlatformTenant,
  createMasterRecord,
  getMasterData,
  reorderLeadStages,
  resetUserPassword,
  rescheduleLeadFollowUp,
  updateMasterRecord,
  updateUser,
  updateLead,
  updateLeadStage,
  restoreLead,
} from "./api";
import counselMateLogo from "./assets/counselmate-logo.png";
import { activities, counselors } from "./data/mockData";

const navItems = [
  { id: "platform", label: "Platform", icon: Users, ownerOnly: true },
  { id: "dashboard", label: "Dashboard", icon: LayoutDashboard },
  { id: "leads", label: "Leads", icon: Search },
  { id: "pipeline", label: "Pipeline", icon: BarChart3 },
  { id: "followups", label: "Follow-ups", icon: CalendarDays },
  { id: "counselors", label: "Counsellors", icon: Users },
  { id: "reports", label: "Reports", icon: FileText },
  { id: "settings", label: "Settings", icon: Settings },
];

function App() {
  const [activePage, setActivePage] = useState("dashboard");
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [currentUser, setCurrentUser] = useState(() => getStoredAuth().user);
  const [authStatus, setAuthStatus] = useState({
    loading: Boolean(getStoredAuth().token),
    error: "",
    signingIn: false,
  });
  const [authView, setAuthView] = useState("login");
  const [forgotStatus, setForgotStatus] = useState({
    submitting: false,
    error: "",
    fieldErrors: {},
    message: "",
  });
  const [passwordDialogOpen, setPasswordDialogOpen] = useState(false);
  const [passwordStatus, setPasswordStatus] = useState({
    saving: false,
    error: "",
    fieldErrors: {},
    message: "",
  });
  const [crmData, setCrmData] = useState({
    dashboard: null,
    leads: emptyLeadList(),
    pipeline: [],
    followUps: [],
    leadOptions: emptyLeadOptions(),
  });
  const [leadFilters, setLeadFilters] = useState(() => defaultLeadFilters());
  const [crmStatus, setCrmStatus] = useState({
    loading: true,
    error: "",
  });
  const [leadModalOpen, setLeadModalOpen] = useState(false);
  const [createStatus, setCreateStatus] = useState({
    saving: false,
    error: "",
    fieldErrors: {},
  });
  const [selectedLeadId, setSelectedLeadId] = useState("");
  const [leadDetail, setLeadDetail] = useState(null);
  const [leadDetailStatus, setLeadDetailStatus] = useState({
    loading: false,
    error: "",
  });
  const [leadActionStatus, setLeadActionStatus] = useState({
    saving: false,
    error: "",
    fieldErrors: {},
  });
  const [platformTenants, setPlatformTenants] = useState([]);
  const [platformStatus, setPlatformStatus] = useState({
    loading: false,
    error: "",
    saving: false,
    fieldErrors: {},
  });
  const [tenantUsers, setTenantUsers] = useState([]);
  const [usersStatus, setUsersStatus] = useState({
    loading: false,
    error: "",
    saving: false,
    fieldErrors: {},
    message: "",
  });
  const activeLabel = navItems.find((item) => item.id === activePage)?.label || "Dashboard";

  const loadCrmData = useCallback(async () => {
    if (!currentUser) {
      return;
    }

    setCrmStatus({ loading: true, error: "" });
    try {
      const data = await getCrmData();
      setCrmData(data);
      setCrmStatus({ loading: false, error: "" });
    } catch (error) {
      if (error?.status === 401) {
        logout();
        setCurrentUser(null);
        setAuthStatus({ loading: false, error: "Your session expired. Sign in again.", signingIn: false });
      }

      setCrmStatus({
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load CRM data.",
      });
    }
  }, [currentUser]);

  const loadLeadList = useCallback(async (filters = leadFilters) => {
    if (!currentUser) {
      return;
    }

    setCrmStatus((current) => ({ ...current, loading: true, error: "" }));
    try {
      const leads = await getLeads(filters);
      setCrmData((current) => ({ ...current, leads }));
      setCrmStatus({ loading: false, error: "" });
    } catch (error) {
      setCrmStatus({
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load leads.",
      });
    }
  }, [currentUser, leadFilters]);

  useEffect(() => {
    const restoreSession = async () => {
      const { token } = getStoredAuth();
      if (!token) {
        setAuthStatus({ loading: false, error: "", signingIn: false });
        return;
      }

      try {
        const user = await getCurrentUser();
        setCurrentUser(user);
        setAuthStatus({ loading: false, error: "", signingIn: false });
      } catch {
        logout();
        setCurrentUser(null);
        setAuthStatus({ loading: false, error: "Session expired. Sign in again.", signingIn: false });
      }
    };

    restoreSession();
  }, []);

  useEffect(() => {
    if (currentUser) {
      loadCrmData();
    }
  }, [loadCrmData]);

  const handleLogin = async (payload) => {
    setAuthStatus({ loading: false, error: "", signingIn: true });
    try {
      const response = await login(payload);
      setCurrentUser(response.user);
      setAuthStatus({ loading: false, error: "", signingIn: false });
    } catch (error) {
      setAuthStatus({
        loading: false,
        error: error instanceof Error ? error.message : "Unable to sign in.",
        signingIn: false,
      });
    }
  };

  const handleForgotPassword = async (payload) => {
    setForgotStatus({ submitting: true, error: "", fieldErrors: {}, message: "" });
    try {
      const response = await forgotPassword(payload);
      setForgotStatus({
        submitting: false,
        error: "",
        fieldErrors: {},
        message: response.message || "If the account exists, password reset instructions are available.",
      });
      return true;
    } catch (error) {
      setForgotStatus({
        submitting: false,
        error: error instanceof Error ? error.message : "Unable to request password reset.",
        fieldErrors: error?.errors || {},
        message: "",
      });
      return false;
    }
  };

  const handleChangePassword = async (payload) => {
    setPasswordStatus({ saving: true, error: "", fieldErrors: {}, message: "" });
    try {
      const response = await changePassword(payload);
      setPasswordStatus({
        saving: false,
        error: "",
        fieldErrors: {},
        message: response.message || "Password changed successfully.",
      });
      return true;
    } catch (error) {
      setPasswordStatus({
        saving: false,
        error: error instanceof Error ? error.message : "Unable to change password.",
        fieldErrors: error?.errors || {},
        message: "",
      });
      return false;
    }
  };

  const handleLogout = () => {
    logout();
    setCurrentUser(null);
    setAuthView("login");
    setPasswordDialogOpen(false);
    setPasswordStatus({ saving: false, error: "", fieldErrors: {}, message: "" });
    setLeadModalOpen(false);
    setSelectedLeadId("");
    setLeadDetail(null);
    setLeadFilters(defaultLeadFilters());
    setCrmData({
      dashboard: null,
      leads: emptyLeadList(),
      pipeline: [],
      followUps: [],
      leadOptions: emptyLeadOptions(),
    });
  };

  const loadPlatformTenants = useCallback(async () => {
    if (!currentUser || currentUser.role !== "Owner") {
      return;
    }

    setPlatformStatus((current) => ({ ...current, loading: true, error: "" }));
    try {
      const tenants = await getPlatformTenants();
      setPlatformTenants(tenants);
      setPlatformStatus((current) => ({ ...current, loading: false, error: "" }));
    } catch (error) {
      setPlatformStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load tenants.",
      }));
    }
  }, [currentUser]);

  useEffect(() => {
    if (activePage === "platform") {
      loadPlatformTenants();
    }
  }, [activePage, loadPlatformTenants]);

  const handleCreateTenant = async (payload) => {
    setPlatformStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {} }));
    try {
      await createPlatformTenant(payload);
      await loadPlatformTenants();
      setPlatformStatus((current) => ({ ...current, saving: false, error: "", fieldErrors: {} }));
      return true;
    } catch (error) {
      setPlatformStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to create tenant.",
        fieldErrors: error?.errors || {},
      }));
      return false;
    }
  };

  const loadTenantUsers = useCallback(async (options = {}) => {
    if (!currentUser) {
      return false;
    }

    const silent = options?.silent === true;
    setUsersStatus((current) => ({
      ...current,
      loading: silent ? current.loading : true,
      error: "",
      message: silent ? current.message : "",
    }));
    try {
      const users = await getUsers();
      setTenantUsers(users);
      setUsersStatus((current) => ({ ...current, loading: false, error: "" }));
      return true;
    } catch (error) {
      setUsersStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load users.",
      }));
      return false;
    }
  }, [currentUser]);

  useEffect(() => {
    if (activePage === "counselors") {
      loadTenantUsers();
    }
  }, [activePage, loadTenantUsers]);

  const runUserAction = async (action, getSuccessMessage) => {
    setUsersStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {}, message: "" }));
    try {
      const response = await action();
      const refreshed = await loadTenantUsers({ silent: true });
      setUsersStatus((current) => ({
        ...current,
        saving: false,
        fieldErrors: {},
        message: getSuccessMessage(response),
        error: refreshed ? "" : current.error,
      }));
      return true;
    } catch (error) {
      setUsersStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to save user.",
        fieldErrors: error?.errors || {},
        message: "",
      }));
      return false;
    }
  };

  const handleCreateUser = (payload) => {
    return runUserAction(
      () => createUser(payload),
      (user) => `${user.fullName} was added to the team.`,
    );
  };

  const handleUpdateUser = (userId, payload) => {
    return runUserAction(
      () => updateUser(userId, payload),
      (user) => `${user.fullName}'s access was updated.`,
    );
  };

  const handleResetUserPassword = (userId, payload) => {
    return runUserAction(
      () => resetUserPassword(userId, payload),
      (response) => response.message || "Password reset successfully.",
    );
  };

  const clearUsersStatus = () => {
    setUsersStatus((current) => ({ ...current, error: "", fieldErrors: {}, message: "" }));
  };

  const handleCreateLead = async (payload) => {
    setCreateStatus({ saving: true, error: "", fieldErrors: {} });
    try {
      await createLead(payload);
      setLeadModalOpen(false);
      setActivePage("leads");
      await loadCrmData();
      await loadLeadList();
      setCreateStatus({ saving: false, error: "", fieldErrors: {} });
    } catch (error) {
      setCreateStatus({
        saving: false,
        error: error instanceof Error ? error.message : "Unable to create lead.",
        fieldErrors: error?.errors || {},
      });
    }
  };

  const openLeadDetail = async (leadId) => {
    setSelectedLeadId(leadId);
    setLeadDetail(null);
    setLeadDetailStatus({ loading: true, error: "" });
    setLeadActionStatus({ saving: false, error: "", fieldErrors: {} });

    try {
      const detail = await getLeadDetail(leadId);
      setLeadDetail(detail);
      setLeadDetailStatus({ loading: false, error: "" });
    } catch (error) {
      setLeadDetailStatus({
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load lead details.",
      });
    }
  };

  const closeLeadDetail = () => {
    if (!leadActionStatus.saving) {
      setSelectedLeadId("");
      setLeadDetail(null);
      setLeadDetailStatus({ loading: false, error: "" });
      setLeadActionStatus({ saving: false, error: "", fieldErrors: {} });
    }
  };

  const runLeadAction = async (action) => {
    if (!selectedLeadId) {
      return;
    }

    setLeadActionStatus({ saving: true, error: "", fieldErrors: {} });
    try {
      const detail = await action(selectedLeadId);
      setLeadDetail(detail);
      await loadCrmData();
      await loadLeadList();
      setLeadActionStatus({ saving: false, error: "", fieldErrors: {} });
    } catch (error) {
      setLeadActionStatus({
        saving: false,
        error: error instanceof Error ? error.message : "Unable to update lead.",
        fieldErrors: error?.errors || {},
      });
    }
  };

  const handleLeadFiltersChange = async (updates) => {
    const nextFilters = { ...leadFilters, ...updates };
    if (!Object.prototype.hasOwnProperty.call(updates, "page")) {
      nextFilters.page = 1;
    }
    setLeadFilters(nextFilters);
    await loadLeadList(nextFilters);
  };

  const handleLeadFiltersReset = async () => {
    const nextFilters = defaultLeadFilters();
    setLeadFilters(nextFilters);
    await loadLeadList(nextFilters);
  };

  if (authStatus.loading) {
    return <StatePanel title="Checking session" message="Validating your CounselMate login..." />;
  }

  if (!currentUser) {
    return authView === "forgot" ? (
      <ForgotPasswordScreen
        status={forgotStatus}
        onSubmit={handleForgotPassword}
        onBack={() => {
          setAuthView("login");
          setForgotStatus({ submitting: false, error: "", fieldErrors: {}, message: "" });
        }}
      />
    ) : (
      <LoginScreen
        error={authStatus.error}
        signingIn={authStatus.signingIn}
        onSubmit={handleLogin}
        onForgot={() => {
          setAuthStatus((current) => ({ ...current, error: "" }));
          setAuthView("forgot");
        }}
      />
    );
  }

  const canManageLeads = ["Owner", "Admin", "BranchManager", "Counselor", "Telecaller"].includes(currentUser.role);
  const canManageUsers = ["Owner", "Admin"].includes(currentUser.role);

  return (
    <div className="app-shell">
      <aside className={`sidebar ${sidebarOpen ? "is-open" : ""}`}>
        <div className="brand">
          <img src={counselMateLogo} alt="CounselMate CRM" />
        </div>

        <nav className="nav-list">
          {navItems.filter((item) => !item.ownerOnly || currentUser.role === "Owner").map((item) => {
            const Icon = item.icon;
            return (
              <button
                key={item.id}
                className={`nav-item ${activePage === item.id ? "active" : ""}`}
                onClick={() => {
                  setActivePage(item.id);
                  setSidebarOpen(false);
                }}
              >
                <Icon size={20} />
                {item.label}
              </button>
            );
          })}
        </nav>

        <button className="sidebar-action" onClick={() => setLeadModalOpen(true)} disabled={!canManageLeads}>
          <Plus size={18} />
          New Lead
        </button>
      </aside>

      <main className="main">
        <header className="topbar">
          <button className="icon-button mobile-only" onClick={() => setSidebarOpen(true)}>
            <Menu size={20} />
          </button>
          <div className="global-search">
            <Search size={20} />
            <input placeholder={`Search ${activeLabel.toLowerCase()}, leads, or tasks...`} />
          </div>
          <div className="topbar-actions">
            <button className="icon-button">
              <Bell size={20} />
              <span className="dot" />
            </button>
            <div className="profile">
              <div>
                <strong>{currentUser.fullName}</strong>
                <span>{currentUser.role}</span>
              </div>
              <div className="avatar">{initials(currentUser.fullName)}</div>
            </div>
            <button className="icon-button" onClick={() => setPasswordDialogOpen(true)} aria-label="Change password">
              <KeyRound size={20} />
            </button>
            <button className="icon-button" onClick={handleLogout} aria-label="Sign out">
              <LogOut size={20} />
            </button>
          </div>
        </header>

        <section className="content">
          {activePage === "platform" && currentUser.role === "Owner" && (
            <PlatformPage
              tenants={platformTenants}
              loading={platformStatus.loading}
              error={platformStatus.error}
              saving={platformStatus.saving}
              fieldErrors={platformStatus.fieldErrors}
              onRetry={loadPlatformTenants}
              onCreateTenant={handleCreateTenant}
            />
          )}
          {activePage === "dashboard" && (
            <Dashboard
              dashboard={crmData.dashboard}
              followUps={crmData.followUps}
              pipeline={crmData.pipeline}
              loading={crmStatus.loading}
              error={crmStatus.error}
              onRetry={loadCrmData}
              onNewLead={() => setLeadModalOpen(true)}
              canManageLeads={canManageLeads}
            />
          )}
          {activePage === "leads" && (
            <LeadsPage
              leads={crmData.leads}
              options={crmData.leadOptions}
              filters={leadFilters}
              loading={crmStatus.loading}
              error={crmStatus.error}
              onRetry={() => loadLeadList()}
              onFiltersChange={handleLeadFiltersChange}
              onResetFilters={handleLeadFiltersReset}
              onNewLead={() => setLeadModalOpen(true)}
              onOpenLead={openLeadDetail}
              canManageLeads={canManageLeads}
            />
          )}
          {activePage === "pipeline" && (
            <PipelinePage
              pipeline={crmData.pipeline}
              loading={crmStatus.loading}
              error={crmStatus.error}
              onRetry={loadCrmData}
              onNewLead={() => setLeadModalOpen(true)}
              onOpenLead={openLeadDetail}
              canManageLeads={canManageLeads}
            />
          )}
          {activePage === "followups" && (
            <FollowUpsPage
              followUps={crmData.followUps}
              loading={crmStatus.loading}
              error={crmStatus.error}
              onRetry={loadCrmData}
            />
          )}
          {activePage === "counselors" && (
            <CounselorsPage
              users={tenantUsers}
              branches={crmData.leadOptions.branches}
              currentUser={currentUser}
              loading={usersStatus.loading}
              error={usersStatus.error}
              saving={usersStatus.saving}
              fieldErrors={usersStatus.fieldErrors}
              message={usersStatus.message}
              canManageUsers={canManageUsers}
              onRetry={() => loadTenantUsers()}
              onClearStatus={clearUsersStatus}
              onCreateUser={handleCreateUser}
              onUpdateUser={handleUpdateUser}
              onResetPassword={handleResetUserPassword}
            />
          )}
          {activePage === "reports" && <ReportsPage />}
          {activePage === "settings" && (
            <SettingsPage currentUser={currentUser} onMasterDataChanged={loadCrmData} />
          )}
        </section>
      </main>

      {leadModalOpen && canManageLeads && (
        <AddLeadModal
          options={crmData.leadOptions}
          saving={createStatus.saving}
          error={createStatus.error}
          fieldErrors={createStatus.fieldErrors}
          onClose={() => {
            if (!createStatus.saving) {
              setLeadModalOpen(false);
              setCreateStatus({ saving: false, error: "", fieldErrors: {} });
            }
          }}
          onSubmit={handleCreateLead}
        />
      )}

      {selectedLeadId && (
        <LeadDetailDrawer
          leadId={selectedLeadId}
          lead={leadDetail}
          options={crmData.leadOptions}
          loading={leadDetailStatus.loading}
          error={leadDetailStatus.error}
          actionStatus={leadActionStatus}
          onClose={closeLeadDetail}
          onRetry={() => openLeadDetail(selectedLeadId)}
          onUpdate={(payload) => runLeadAction((leadId) => updateLead(leadId, payload))}
          onAssign={(payload) => runLeadAction((leadId) => assignLead(leadId, payload))}
          onStageChange={(payload) => runLeadAction((leadId) => updateLeadStage(leadId, payload))}
          onArchive={(payload) => runLeadAction((leadId) => archiveLead(leadId, payload))}
          onRestore={(payload) => runLeadAction((leadId) => restoreLead(leadId, payload))}
          onAddActivity={(payload) => runLeadAction((leadId) => addLeadActivity(leadId, payload))}
          onCreateFollowUp={(payload) => runLeadAction((leadId) => createLeadFollowUp(leadId, payload))}
          onRescheduleFollowUp={(followUpId, payload) => runLeadAction((leadId) => rescheduleLeadFollowUp(leadId, followUpId, payload))}
          onCancelFollowUp={(followUpId, payload) => runLeadAction((leadId) => cancelLeadFollowUp(leadId, followUpId, payload))}
          onCompleteFollowUp={(followUpId, payload) => runLeadAction((leadId) => completeLeadFollowUp(leadId, followUpId, payload))}
          canManageLeads={canManageLeads}
        />
      )}

      {passwordDialogOpen && (
        <ChangePasswordModal
          status={passwordStatus}
          onClose={() => {
            if (!passwordStatus.saving) {
              setPasswordDialogOpen(false);
              setPasswordStatus({ saving: false, error: "", fieldErrors: {}, message: "" });
            }
          }}
          onSubmit={handleChangePassword}
        />
      )}
    </div>
  );
}

function LoginScreen({ error, signingIn, onSubmit, onForgot }) {
  const [form, setForm] = useState({
    email: "",
    password: "",
    remember: true,
  });
  const [passwordVisible, setPasswordVisible] = useState(false);

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      email: form.email.trim(),
      password: form.password,
    });
  };

  return (
    <main className="auth-shell">
      <section className="auth-window auth-window-login" aria-label="CounselMate login">
        <div className="auth-split">
          <AuthArtPanel
            title="Manage Smarter. Grow Faster. Connect Anywhere."
            text="From admissions inquiries to enrolled students, your CRM keeps every workflow moving across teams."
            showTopBar
          />

          <div className="auth-form-zone">
          <form className="auth-form-panel" onSubmit={handleSubmit} noValidate>
            <div className="auth-form-header">
              <div>
                <h1>Welcome Back!</h1>
                <p>Log in to start managing your CRM workspace.</p>
              </div>
            </div>

            {error && <div className="form-alert">{error}</div>}

            <Field label="Email" required>
              <input value={form.email} type="email" maxLength={240} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} placeholder="Input your email" required autoFocus />
            </Field>

            <Field label="Password" required>
              <div className="password-input">
                <input
                  value={form.password}
                  type={passwordVisible ? "text" : "password"}
                  maxLength={120}
                  onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
                  placeholder="Input your password"
                  required
                />
                <button type="button" onClick={() => setPasswordVisible((current) => !current)} aria-label={passwordVisible ? "Hide password" : "Show password"}>
                  {passwordVisible ? <EyeOff size={20} /> : <Eye size={20} />}
                </button>
              </div>
            </Field>

            <div className="auth-options">
              <label className="check-row">
                <input type="checkbox" checked={form.remember} onChange={(event) => setForm((current) => ({ ...current, remember: event.target.checked }))} />
              Remember Me
              </label>
              <button type="button" className="link-button" onClick={onForgot}>Forgot Password?</button>
            </div>

            <button className="auth-submit" type="submit" disabled={signingIn}>
              {signingIn ? "Logging in..." : "Login"}
            </button>

            <div className="auth-divider"><span>Or continue with:</span></div>

            <button className="google-auth-button" type="button" disabled>
              <span>G</span>
              Continue with Google
            </button>

            <p className="auth-signup">Don't have an account? <span>Sign up here</span></p>
          </form>
          </div>
        </div>
      </section>
    </main>
  );
}

function ForgotPasswordScreen({ status, onSubmit, onBack }) {
  const [form, setForm] = useState({
    email: "",
  });
  const getFieldError = (field) => firstError(status.fieldErrors[field]);

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      email: form.email.trim(),
    });
  };

  return (
    <main className="auth-shell">
      <section className="auth-window auth-window-reset" aria-label="Reset password">
        <div className="auth-split">
          <AuthArtPanel
            title="Forgot Your Password?"
            text="Enter your registered email address to regain access to your CRM dashboard."
            showTopBar
          />

          <div className="auth-form-zone">
          <form className="auth-form-panel auth-reset-panel" onSubmit={handleSubmit} noValidate>
            <div className="auth-form-header compact">
              <h1>Reset Password</h1>
            </div>

            {status.error && <div className="form-alert">{status.error}</div>}
            {status.message && <div className="form-success">{status.message}</div>}

            <Field label="Email Address" error={getFieldError("email")} required>
              <input
                value={form.email}
                type="email"
                maxLength={240}
                onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
                placeholder="Enter your email address"
                required
                autoFocus
              />
            </Field>

            <button className="auth-submit" type="submit" disabled={status.submitting}>
              {status.submitting ? "Sending..." : "Send Reset Link"}
            </button>

            <button type="button" className="link-button auth-back-link" onClick={onBack}>
              Back to Login
            </button>
          </form>
          </div>
        </div>
      </section>
    </main>
  );
}

function AuthArtPanel({ title, text, showTopBar = false }) {
  return (
    <aside className="auth-art-panel">
      <div className="auth-polygons" aria-hidden="true">
        <span />
        <span />
        <span />
        <span />
        <span />
        <span />
      </div>
      {showTopBar && (
        <div className="auth-art-top">
          <img src={counselMateLogo} alt="CounselMate CRM" />
          <button type="button" className="auth-back-website">Back to Website</button>
        </div>
      )}
      <div className="auth-art-content">
        <h2>{title}</h2>
        <p>{text}</p>
      </div>
    </aside>
  );
}

function ChangePasswordModal({ status, onClose, onSubmit }) {
  const [form, setForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [clientErrors, setClientErrors] = useState({});
  const [visible, setVisible] = useState({
    currentPassword: false,
    newPassword: false,
    confirmPassword: false,
  });

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setClientErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  };
  const getFieldError = (field) => clientErrors[field] || firstError(status.fieldErrors[field]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    const validationErrors = validatePasswordChangeForm(form);
    setClientErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    const changed = await onSubmit(form);
    if (changed) {
      setForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
    }
  };

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !status.saving && onClose()}>
      <section className="modal password-modal" role="dialog" aria-modal="true" aria-labelledby="change-password-title">
        <header className="modal-header">
          <div>
            <h2 id="change-password-title">Change Password</h2>
            <p>Update your account password.</p>
          </div>
          <button className="icon-button" onClick={onClose} disabled={status.saving} aria-label="Close change password form">
            <X size={20} />
          </button>
        </header>

        <form className="lead-form" onSubmit={handleSubmit} noValidate>
          {status.error && <div className="form-alert">{status.error}</div>}
          {status.message && <div className="form-success">{status.message}</div>}

          <PasswordField
            label="Current Password"
            value={form.currentPassword}
            visible={visible.currentPassword}
            error={getFieldError("currentPassword")}
            onToggle={() => setVisible((current) => ({ ...current, currentPassword: !current.currentPassword }))}
            onChange={(value) => updateField("currentPassword", value)}
            autoFocus
          />

          <PasswordField
            label="New Password"
            value={form.newPassword}
            visible={visible.newPassword}
            error={getFieldError("newPassword")}
            onToggle={() => setVisible((current) => ({ ...current, newPassword: !current.newPassword }))}
            onChange={(value) => updateField("newPassword", value)}
          />

          <PasswordField
            label="Confirm Password"
            value={form.confirmPassword}
            visible={visible.confirmPassword}
            error={getFieldError("confirmPassword")}
            onToggle={() => setVisible((current) => ({ ...current, confirmPassword: !current.confirmPassword }))}
            onChange={(value) => updateField("confirmPassword", value)}
          />

          <footer className="modal-actions">
            <button type="button" className="ghost-button" onClick={onClose} disabled={status.saving}>Cancel</button>
            <button type="submit" className="primary-button" disabled={status.saving}>{status.saving ? "Saving..." : "Change Password"}</button>
          </footer>
        </form>
      </section>
    </div>
  );
}

function PasswordField({ label, value, visible, error, onToggle, onChange, autoFocus = false, className = "" }) {
  return (
    <Field label={label} error={error} required className={className}>
      <div className="password-input">
        <input
          value={value}
          type={visible ? "text" : "password"}
          maxLength={120}
          onChange={(event) => onChange(event.target.value)}
          required
          autoFocus={autoFocus}
        />
        <button type="button" onClick={onToggle} aria-label={visible ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}>
          {visible ? <EyeOff size={20} /> : <Eye size={20} />}
        </button>
      </div>
    </Field>
  );
}

function PlatformPage({ tenants, loading, error, saving, fieldErrors, onRetry, onCreateTenant }) {
  const [formOpen, setFormOpen] = useState(false);

  return (
    <>
      <PageTitle
        title="Platform Tenants"
        subtitle="Create and monitor client institutes on CounselMate."
        action={
          <button className="primary-button" onClick={() => setFormOpen(true)}>
            <Plus size={18} />
            New Client
          </button>
        }
      />

      {formOpen && (
        <CreateTenantPanel
          saving={saving}
          error={error}
          fieldErrors={fieldErrors}
          onCancel={() => setFormOpen(false)}
          onSubmit={async (payload) => {
            const created = await onCreateTenant(payload);
            if (created) {
              setFormOpen(false);
            }
          }}
        />
      )}

      <div className="table-card">
        {loading && <StatePanel title="Loading tenants" message="Fetching client institutes..." />}
        {error && !formOpen && <StatePanel title="Could not load tenants" message={error} action={onRetry} />}
        {!loading && !error && tenants.length === 0 && <StatePanel title="No tenants" message="Create the first client institute." />}
        {!loading && tenants.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Institute</th>
                <th>Slug</th>
                <th>Status</th>
                <th>Users</th>
                <th>Leads</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {tenants.map((tenant) => (
                <tr key={tenant.id}>
                  <td><strong>{tenant.name}</strong></td>
                  <td>{tenant.slug}</td>
                  <td><Badge label={tenant.isActive ? "Active" : "Inactive"} muted={!tenant.isActive} /></td>
                  <td>{tenant.activeUsers}</td>
                  <td>{tenant.leads}</td>
                  <td>{formatDate(tenant.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

function CreateTenantPanel({ saving, error, fieldErrors, onCancel, onSubmit }) {
  const [form, setForm] = useState({
    name: "",
    slug: "",
    branchName: "Main Branch",
    city: "",
    adminFullName: "",
    adminEmail: "",
    adminPassword: "Demo@12345",
  });

  const updateField = (field, value) => {
    setForm((current) => {
      const next = { ...current, [field]: value };
      if (field === "name" && !current.slug) {
        next.slug = value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
      }
      return next;
    });
  };

  const getFieldError = (field) => firstError(fieldErrors[field]);

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      name: form.name.trim(),
      slug: form.slug.trim(),
      branchName: form.branchName.trim(),
      city: form.city.trim(),
      adminFullName: form.adminFullName.trim(),
      adminEmail: form.adminEmail.trim(),
      adminPassword: form.adminPassword,
    });
  };

  return (
    <form className="tenant-create-panel" onSubmit={handleSubmit}>
      <div className="section-heading">
        <h3>Create Client Institute</h3>
        <button type="button" className="ghost-button" onClick={onCancel} disabled={saving}>Cancel</button>
      </div>
      {error && <div className="form-alert">{error}</div>}
      <div className="form-grid">
        <Field label="Institute Name" error={getFieldError("name")} required>
          <input value={form.name} maxLength={160} onChange={(event) => updateField("name", event.target.value)} required autoFocus />
        </Field>
        <Field label="Tenant Slug" error={getFieldError("slug")} required>
          <input value={form.slug} maxLength={80} onChange={(event) => updateField("slug", event.target.value)} required />
        </Field>
        <Field label="Branch Name" error={getFieldError("branchName")} required>
          <input value={form.branchName} maxLength={160} onChange={(event) => updateField("branchName", event.target.value)} required />
        </Field>
        <Field label="City" error={getFieldError("city")} required>
          <input value={form.city} maxLength={120} onChange={(event) => updateField("city", event.target.value)} required />
        </Field>
        <Field label="Admin Full Name" error={getFieldError("adminFullName")} required>
          <input value={form.adminFullName} maxLength={160} onChange={(event) => updateField("adminFullName", event.target.value)} required />
        </Field>
        <Field label="Admin Email" error={getFieldError("adminEmail")} required>
          <input value={form.adminEmail} type="email" maxLength={240} onChange={(event) => updateField("adminEmail", event.target.value)} required />
        </Field>
        <Field label="Initial Password" error={getFieldError("adminPassword")} className="span-2" required>
          <input value={form.adminPassword} type="password" maxLength={120} onChange={(event) => updateField("adminPassword", event.target.value)} required />
        </Field>
      </div>
      <footer className="modal-actions">
        <button type="submit" className="primary-button" disabled={saving}>{saving ? "Creating..." : "Create Client"}</button>
      </footer>
    </form>
  );
}

function PageTitle({ title, subtitle, action }) {
  return (
    <div className="page-title">
      <div>
        <h1>{title}</h1>
        <p>{subtitle}</p>
      </div>
      {action}
    </div>
  );
}

function Dashboard({ dashboard, followUps, pipeline, loading, error, onRetry, onNewLead, canManageLeads }) {
  const barHeights = useMemo(() => {
    const counts = pipeline.map((stage) => stage.count);
    const max = Math.max(...counts, 1);
    return counts.length ? counts.map((count) => Math.max(8, Math.round((count / max) * 100))) : [8, 8, 8, 8, 8];
  }, [pipeline]);

  return (
    <>
      <PageTitle
        title="Counsellor Dashboard"
        subtitle="Overview of lead flow, admissions, follow-ups, and counsellor productivity."
        action={
          <button className="primary-button" onClick={onNewLead} disabled={!canManageLeads}>
            <Plus size={18} />
            New Lead
          </button>
        }
      />

      <div className="metric-grid">
        <Metric title="Total Leads" value={formatNumber(dashboard?.totalLeads)} />
        <Metric title="New Leads" value={formatNumber(dashboard?.newLeadsToday)} warning />
        <Metric title="Contacted" value={formatNumber(dashboard?.contacted)} />
        <Metric title="Enrolled" value={formatNumber(dashboard?.enrolled)} />
      </div>

      <div className="dashboard-grid">
        <Card title="Conversion Pipeline" className="wide-card">
          {loading && <StatePanel title="Loading pipeline" message="Fetching live stage counts..." />}
          {error && <StatePanel title="Could not load pipeline" message={error} action={onRetry} />}
          {!loading && !error && (
            <div className="bar-chart">
              {barHeights.map((height, index) => (
                <span key={pipeline[index]?.name || index} style={{ height: `${height}%` }} />
              ))}
            </div>
          )}
        </Card>

        <Card title="Today's Schedule" badge={`${followUps.length} Pending`}>
          {loading && <StatePanel title="Loading follow-ups" message="Fetching the live queue..." />}
          {error && <StatePanel title="Could not load follow-ups" message={error} action={onRetry} />}
          {!loading && !error && followUps.length === 0 && <StatePanel title="No follow-ups" message="There are no scheduled follow-ups for this tenant." />}
          {!loading && !error && followUps.length > 0 && (
            <div className="schedule-list">
              {followUps.slice(0, 3).map((item) => (
                <FollowUpRow key={item.id} item={item} compact />
              ))}
            </div>
          )}
        </Card>

        <Card title="Recent Activity" className="wide-card">
          <div className="activity-list">
            {activities.map((activity, index) => (
              <div className="activity-item" key={activity}>
                <span className={`activity-icon tone-${index}`} />
                <div>
                  <strong>{activity.split(" ")[0]} {activity.split(" ")[1]}</strong>
                  <p>{activity}</p>
                  <small>{index + 1} hour{index ? "s" : ""} ago</small>
                </div>
              </div>
            ))}
          </div>
        </Card>

        <Card title="Top Performing Channels" className="wide-card full-row">
          <SourceTable />
        </Card>
      </div>
    </>
  );
}

function LeadsPage({ leads, options, filters, loading, error, onRetry, onFiltersChange, onResetFilters, onNewLead, onOpenLead, canManageLeads }) {
  const items = leads?.items || [];
  const total = leads?.total || 0;
  return (
    <>
      <PageTitle
        title="Leads Management"
        subtitle="Review and manage student admission pipelines."
        action={
          <button className="primary-button" onClick={onNewLead} disabled={!canManageLeads}>
            <Plus size={18} />
            Add Lead
          </button>
        }
      />
      <FilterBar
        filters={filters}
        options={options}
        total={total}
        loading={loading}
        onChange={onFiltersChange}
        onReset={onResetFilters}
      />
      <LeadsTable
        leads={items}
        page={leads?.page || 1}
        pageSize={leads?.pageSize || filters.pageSize}
        total={total}
        loading={loading}
        error={error}
        onRetry={onRetry}
        onOpenLead={onOpenLead}
        onPageChange={(page) => onFiltersChange({ page })}
      />
    </>
  );
}

function AddLeadModal({ options, saving, error, fieldErrors, onClose, onSubmit }) {
  const [form, setForm] = useState(() => createDefaultLeadForm(options));
  const [clientErrors, setClientErrors] = useState({});
  const hasRequiredOptions = options.courses.length > 0 && options.sources.length > 0 && options.stages.length > 0;

  useEffect(() => {
    setForm((current) => ({
      ...current,
      courseId: current.courseId || options.courses[0]?.id || "",
      leadSourceId: current.leadSourceId || options.sources[0]?.id || "",
      leadStageId: current.leadStageId || findOptionId(options.stages, "New Inquiry") || options.stages[0]?.id || "",
      branchId: current.branchId || options.branches[0]?.id || "",
      assignedUserId: current.assignedUserId || options.counselors[0]?.id || "",
    }));
  }, [options]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === "Escape" && !saving) {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose, saving]);

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setClientErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const getFieldError = (field) => {
    return clientErrors[field] || firstError(fieldErrors[field]);
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const validationErrors = validateLeadForm(form);
    setClientErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    onSubmit({
      studentName: form.studentName.trim(),
      guardianName: optionalValue(form.guardianName),
      email: form.email.trim(),
      phone: form.phone.trim(),
      city: optionalValue(form.city),
      courseId: form.courseId,
      leadSourceId: form.leadSourceId,
      leadStageId: form.leadStageId,
      branchId: optionalValue(form.branchId),
      assignedUserId: optionalValue(form.assignedUserId),
      status: form.status,
      priority: form.priority,
      nextFollowUpAt: form.nextFollowUpAt ? new Date(form.nextFollowUpAt).toISOString() : null,
    });
  };

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !saving && onClose()}>
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="add-lead-title">
        <header className="modal-header">
          <div>
            <h2 id="add-lead-title">Add Lead</h2>
            <p>Create a tenant-scoped admission inquiry.</p>
          </div>
          <button className="icon-button" onClick={onClose} disabled={saving} aria-label="Close add lead form">
            <X size={20} />
          </button>
        </header>

        {!hasRequiredOptions && (
          <StatePanel title="Lead setup incomplete" message="Add at least one active course, source, and stage before creating leads." />
        )}

        {hasRequiredOptions && (
          <form className="lead-form" onSubmit={handleSubmit} noValidate>
            {error && <div className="form-alert">{error}</div>}

            <div className="form-grid">
              <Field label="Student Name" error={getFieldError("studentName")} required>
                <input value={form.studentName} maxLength={160} onChange={(event) => updateField("studentName", event.target.value)} autoFocus required />
              </Field>

              <Field label="Guardian Name" error={getFieldError("guardianName")}>
                <input value={form.guardianName} maxLength={160} onChange={(event) => updateField("guardianName", event.target.value)} />
              </Field>

              <Field label="Email" error={getFieldError("email")} required>
                <input value={form.email} type="email" maxLength={240} onChange={(event) => updateField("email", event.target.value)} required />
              </Field>

              <Field label="Phone" error={getFieldError("phone")} required>
                <input value={form.phone} maxLength={40} onChange={(event) => updateField("phone", event.target.value)} placeholder="+91 98765 43210" required />
              </Field>

              <Field label="City" error={getFieldError("city")}>
                <input value={form.city} maxLength={120} onChange={(event) => updateField("city", event.target.value)} />
              </Field>

              <Field label="Course" error={getFieldError("courseId")} required>
                <select value={form.courseId} onChange={(event) => updateField("courseId", event.target.value)} required>
                  {options.courses.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </Field>

              <Field label="Source" error={getFieldError("leadSourceId")} required>
                <select value={form.leadSourceId} onChange={(event) => updateField("leadSourceId", event.target.value)} required>
                  {options.sources.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </Field>

              <Field label="Stage" error={getFieldError("leadStageId")} required>
                <select value={form.leadStageId} onChange={(event) => updateField("leadStageId", event.target.value)} required>
                  {options.stages.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </Field>

              <Field label="Branch" error={getFieldError("branchId")}>
                <select value={form.branchId} onChange={(event) => updateField("branchId", event.target.value)}>
                  <option value="">No branch</option>
                  {options.branches.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </Field>

              <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                <select value={form.assignedUserId} onChange={(event) => updateField("assignedUserId", event.target.value)}>
                  <option value="">Unassigned</option>
                  {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </Field>

              <Field label="Status" error={getFieldError("status")}>
                <select value={form.status} onChange={(event) => updateField("status", event.target.value)}>
                  {["New Lead", "Interested", "Follow Up", "Enrolled", "Dropped"].map((item) => <option key={item} value={item}>{item}</option>)}
                </select>
              </Field>

              <Field label="Priority" error={getFieldError("priority")}>
                <select value={form.priority} onChange={(event) => updateField("priority", event.target.value)}>
                  {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                </select>
              </Field>

              <Field label="Next Follow-up" error={getFieldError("nextFollowUpAt")} className="span-2">
                <input type="datetime-local" value={form.nextFollowUpAt} onChange={(event) => updateField("nextFollowUpAt", event.target.value)} />
              </Field>
            </div>

            <footer className="modal-actions">
              <button type="button" className="ghost-button" onClick={onClose} disabled={saving}>Cancel</button>
              <button type="submit" className="primary-button" disabled={saving}>{saving ? "Saving..." : "Create Lead"}</button>
            </footer>
          </form>
        )}
      </section>
    </div>
  );
}

function LeadDetailDrawer({
  leadId,
  lead,
  options,
  loading,
  error,
  actionStatus,
  onClose,
  onRetry,
  onUpdate,
  onAssign,
  onStageChange,
  onArchive,
  onRestore,
  onAddActivity,
  onCreateFollowUp,
  onRescheduleFollowUp,
  onCancelFollowUp,
  onCompleteFollowUp,
  canManageLeads,
}) {
  const [editForm, setEditForm] = useState(() => createLeadUpdateForm(lead));
  const [noteForm, setNoteForm] = useState({ type: "Note", description: "" });
  const [followUpForm, setFollowUpForm] = useState(() => createDefaultFollowUpForm(lead));
  const [rescheduleForm, setRescheduleForm] = useState(null);

  useEffect(() => {
    setEditForm(createLeadUpdateForm(lead));
    setFollowUpForm(createDefaultFollowUpForm(lead));
    setRescheduleForm(null);
  }, [lead]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === "Escape" && !actionStatus.saving) {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [actionStatus.saving, onClose]);

  const getFieldError = (field) => firstError(actionStatus.fieldErrors[field]);
  const canSave = Boolean(lead && editForm.studentName.trim() && editForm.email.trim() && editForm.phone.trim() && editForm.courseId && editForm.leadSourceId && editForm.leadStageId);
  const archived = Boolean(lead?.archivedAt);
  const stageOptions = includeCurrentOption(options.stages, lead?.leadStageId, lead?.stage);
  const courseOptions = includeCurrentOption(options.courses, lead?.courseId, lead?.course);
  const sourceOptions = includeCurrentOption(options.sources, lead?.leadSourceId, lead?.source);
  const branchOptions = includeCurrentOption(options.branches, lead?.branchId, lead?.branch);
  const counselorOptions = includeCurrentOption(options.counselors, lead?.assignedUserId, lead?.counselor === "Unassigned" ? "" : lead?.counselor);

  const handleUpdateSubmit = async (event) => {
    event.preventDefault();
    if (!canSave) {
      return;
    }

    await onUpdate({
      studentName: editForm.studentName.trim(),
      guardianName: optionalValue(editForm.guardianName),
      email: editForm.email.trim(),
      phone: editForm.phone.trim(),
      city: optionalValue(editForm.city),
      courseId: editForm.courseId,
      leadSourceId: editForm.leadSourceId,
      leadStageId: editForm.leadStageId,
      branchId: optionalValue(editForm.branchId),
      assignedUserId: optionalValue(editForm.assignedUserId),
      status: editForm.status,
      priority: editForm.priority,
      nextFollowUpAt: editForm.nextFollowUpAt ? new Date(editForm.nextFollowUpAt).toISOString() : null,
      version: lead.version,
    });
  };

  const handleNoteSubmit = async (event) => {
    event.preventDefault();
    if (!noteForm.description.trim()) {
      return;
    }

    await onAddActivity({
      type: noteForm.type,
      description: noteForm.description.trim(),
    });
    setNoteForm({ type: "Note", description: "" });
  };

  const handleFollowUpSubmit = async (event) => {
    event.preventDefault();
    if (!followUpForm.dueAt) {
      return;
    }

    await onCreateFollowUp({
      type: followUpForm.type,
      priority: followUpForm.priority,
      assignedUserId: optionalValue(followUpForm.assignedUserId),
      dueAt: new Date(followUpForm.dueAt).toISOString(),
    });
    setFollowUpForm(createDefaultFollowUpForm(lead));
  };

  const openRescheduleForm = (followUp) => {
    setRescheduleForm({
      id: followUp.id,
      version: followUp.version,
      type: followUp.type || "Call",
      priority: followUp.priority || "Medium",
      assignedUserId: findOptionIdByName(options.counselors, followUp.assignedTo),
      dueAt: toDateTimeLocalValue(followUp.dueAt),
    });
  };

  const handleRescheduleSubmit = async (event) => {
    event.preventDefault();
    if (!rescheduleForm?.id || !rescheduleForm.dueAt) {
      return;
    }

    await onRescheduleFollowUp(rescheduleForm.id, {
      type: rescheduleForm.type,
      priority: rescheduleForm.priority,
      assignedUserId: optionalValue(rescheduleForm.assignedUserId),
      dueAt: new Date(rescheduleForm.dueAt).toISOString(),
      version: rescheduleForm.version,
    });
    setRescheduleForm(null);
  };

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !actionStatus.saving && onClose()}>
      <aside className="lead-drawer" role="dialog" aria-modal="true" aria-labelledby="lead-detail-title">
        <header className="drawer-header">
          <div>
            <span className="eyebrow">{lead?.id || leadId}</span>
            <h2 id="lead-detail-title">{lead?.studentName || "Lead Details"}</h2>
          </div>
          <button className="icon-button" onClick={onClose} disabled={actionStatus.saving} aria-label="Close lead details">
            <X size={20} />
          </button>
        </header>

        {loading && <StatePanel title="Loading lead" message="Fetching the latest lead profile..." />}
        {error && <StatePanel title="Could not load lead" message={error} action={onRetry} />}

        {!loading && !error && lead && (
          <div className="drawer-body">
            {actionStatus.error && <div className="form-alert">{actionStatus.error}</div>}

            <section className="lead-summary">
              <div className="lead-avatar">{initials(lead.studentName)}</div>
              <div>
                <h3>{lead.studentName}</h3>
                <p>{lead.course} · {lead.source}</p>
              </div>
              <Status status={archived ? "Archived" : lead.status} />
            </section>

            <div className="detail-grid">
              <InfoItem label="Phone" value={lead.phone} />
              <InfoItem label="Email" value={lead.email} />
              <InfoItem label="Guardian" value={lead.guardianName || "Not added"} />
              <InfoItem label="City" value={lead.city || "Not added"} />
              <InfoItem label="Branch" value={lead.branch || "No branch"} />
              <InfoItem label="Created" value={formatDate(lead.createdAt)} />
              <InfoItem label="Updated" value={formatDate(lead.updatedAt)} />
            </div>

            <form className="drawer-section" onSubmit={handleUpdateSubmit}>
              <div className="section-heading">
                <h3>Lead Profile</h3>
                <div className="detail-list-actions">
                  {archived ? (
                    <button type="button" className="ghost-button" disabled={!canManageLeads || actionStatus.saving} onClick={() => onRestore({ version: lead.version })}>
                      <RotateCcw size={16} />
                      Restore
                    </button>
                  ) : (
                    <button type="button" className="ghost-button danger-text" disabled={!canManageLeads || actionStatus.saving} onClick={() => onArchive({ version: lead.version })}>
                      <UserX size={16} />
                      Archive
                    </button>
                  )}
                  <button type="submit" className="primary-button" disabled={!canManageLeads || !canSave || actionStatus.saving || archived}>
                    {actionStatus.saving ? "Saving..." : "Save Changes"}
                  </button>
                </div>
              </div>
              {archived && <p className="muted-text">Archived leads are read-only until restored.</p>}
              <div className="form-grid compact">
                <Field label="Student Name" error={getFieldError("studentName")} required>
                  <input value={editForm.studentName} maxLength={160} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, studentName: event.target.value }))} required />
                </Field>
                <Field label="Guardian Name" error={getFieldError("guardianName")}>
                  <input value={editForm.guardianName} maxLength={160} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, guardianName: event.target.value }))} />
                </Field>
                <Field label="Email" error={getFieldError("email")} required>
                  <input type="email" value={editForm.email} maxLength={240} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, email: event.target.value }))} required />
                </Field>
                <Field label="Phone" error={getFieldError("phone")} required>
                  <input value={editForm.phone} maxLength={40} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, phone: event.target.value }))} required />
                </Field>
                <Field label="City" error={getFieldError("city")}>
                  <input value={editForm.city} maxLength={120} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, city: event.target.value }))} />
                </Field>
                <Field label="Course" error={getFieldError("courseId")} required>
                  <select value={editForm.courseId} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, courseId: event.target.value }))} required>
                    {courseOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Source" error={getFieldError("leadSourceId")} required>
                  <select value={editForm.leadSourceId} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, leadSourceId: event.target.value }))} required>
                    {sourceOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Stage" error={getFieldError("leadStageId")} required>
                  <select value={editForm.leadStageId} disabled={archived} onChange={(event) => {
                    const nextStageId = event.target.value;
                    setEditForm((current) => ({ ...current, leadStageId: nextStageId }));
                    if (canManageLeads && nextStageId !== lead.leadStageId) {
                      onStageChange({ leadStageId: nextStageId, status: editForm.status, version: lead.version });
                    }
                  }} required>
                    {stageOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Branch" error={getFieldError("branchId")}>
                  <select value={editForm.branchId} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, branchId: event.target.value }))}>
                    <option value="">No branch</option>
                    {branchOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Status" error={getFieldError("status")}>
                  <select value={editForm.status} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, status: event.target.value }))}>
                    {["New Lead", "Interested", "Follow Up", "Enrolled", "Dropped"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Priority" error={getFieldError("priority")}>
                  <select value={editForm.priority} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, priority: event.target.value }))}>
                    {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                  <select value={editForm.assignedUserId} disabled={archived} onChange={(event) => {
                    const assignedUserId = event.target.value;
                    setEditForm((current) => ({ ...current, assignedUserId }));
                    onAssign({ assignedUserId: optionalValue(assignedUserId), version: lead.version });
                  }}>
                    <option value="">Unassigned</option>
                    {counselorOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Next Follow-up" error={getFieldError("nextFollowUpAt")} className="span-2">
                  <input type="datetime-local" value={editForm.nextFollowUpAt} disabled={archived} onChange={(event) => setEditForm((current) => ({ ...current, nextFollowUpAt: event.target.value }))} />
                </Field>
              </div>
            </form>

            <form className="drawer-section" onSubmit={handleNoteSubmit}>
              <div className="section-heading">
                <h3>Add Activity</h3>
                <button type="submit" className="primary-button" disabled={!canManageLeads || !noteForm.description.trim() || actionStatus.saving}>
                  Add Note
                </button>
              </div>
              <div className="form-grid compact">
                <Field label="Type" error={getFieldError("type")}>
                  <select value={noteForm.type} onChange={(event) => setNoteForm((current) => ({ ...current, type: event.target.value }))}>
                    {["Note", "Call", "WhatsApp", "Email", "Meeting"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Description" error={getFieldError("description")} className="span-2" required>
                  <textarea value={noteForm.description} maxLength={500} onChange={(event) => setNoteForm((current) => ({ ...current, description: event.target.value }))} required />
                </Field>
              </div>
            </form>

            <form className="drawer-section" onSubmit={handleFollowUpSubmit}>
              <div className="section-heading">
                <h3>Schedule Follow-up</h3>
                <button type="submit" className="primary-button" disabled={!canManageLeads || archived || !followUpForm.dueAt || actionStatus.saving}>
                  Schedule
                </button>
              </div>
              <div className="form-grid compact">
                <Field label="Type" error={getFieldError("type")}>
                  <select value={followUpForm.type} onChange={(event) => setFollowUpForm((current) => ({ ...current, type: event.target.value }))}>
                    {["Call", "WhatsApp", "Email", "Walk-in"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Priority" error={getFieldError("priority")}>
                  <select value={followUpForm.priority} onChange={(event) => setFollowUpForm((current) => ({ ...current, priority: event.target.value }))}>
                    {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                  <select value={followUpForm.assignedUserId} onChange={(event) => setFollowUpForm((current) => ({ ...current, assignedUserId: event.target.value }))}>
                    <option value="">Current owner</option>
                    {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                  </select>
                </Field>
                <Field label="Due At" error={getFieldError("dueAt")} required>
                  <input type="datetime-local" value={followUpForm.dueAt} onChange={(event) => setFollowUpForm((current) => ({ ...current, dueAt: event.target.value }))} required />
                </Field>
              </div>
            </form>

            <section className="drawer-section">
              <div className="section-heading">
                <h3>Follow-ups</h3>
                <span>{lead.followUps.length}</span>
              </div>
              {rescheduleForm && (
                <form className="inline-editor" onSubmit={handleRescheduleSubmit}>
                  <Field label="Type" error={getFieldError("type")}>
                    <select value={rescheduleForm.type} onChange={(event) => setRescheduleForm((current) => ({ ...current, type: event.target.value }))}>
                      {["Call", "WhatsApp", "Email", "Walk-in"].map((item) => <option key={item} value={item}>{item}</option>)}
                    </select>
                  </Field>
                  <Field label="Priority" error={getFieldError("priority")}>
                    <select value={rescheduleForm.priority} onChange={(event) => setRescheduleForm((current) => ({ ...current, priority: event.target.value }))}>
                      {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                    </select>
                  </Field>
                  <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                    <select value={rescheduleForm.assignedUserId} onChange={(event) => setRescheduleForm((current) => ({ ...current, assignedUserId: event.target.value }))}>
                      <option value="">Current owner</option>
                      {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                    </select>
                  </Field>
                  <Field label="Due At" error={getFieldError("dueAt")} required>
                    <input type="datetime-local" value={rescheduleForm.dueAt} onChange={(event) => setRescheduleForm((current) => ({ ...current, dueAt: event.target.value }))} required />
                  </Field>
                  <div className="inline-editor-actions">
                    <button type="button" className="ghost-button" onClick={() => setRescheduleForm(null)} disabled={actionStatus.saving}>Cancel</button>
                    <button type="submit" className="primary-button" disabled={actionStatus.saving || !rescheduleForm.dueAt}>{actionStatus.saving ? "Saving..." : "Reschedule"}</button>
                  </div>
                </form>
              )}
              <div className="detail-list">
                {lead.followUps.length === 0 && <p className="muted-text">No follow-ups scheduled.</p>}
                {lead.followUps.map((item) => (
                  <div className="detail-list-item" key={item.id}>
                    <div>
                      <strong>{item.type} - {formatDate(item.dueAt)}</strong>
                      <p>{formatTime(item.dueAt)} - {item.assignedTo}</p>
                      {item.status !== "Scheduled" && (
                        <small>{item.status === "Completed" ? `Completed ${formatFollowUpLabel(item.completedAt)}` : `Cancelled ${formatFollowUpLabel(item.cancelledAt)}`}</small>
                      )}
                    </div>
                    <div className="detail-list-actions">
                      <Badge label={item.status} muted={item.status !== "Completed"} />
                      {canManageLeads && item.status === "Scheduled" && !archived && (
                        <>
                          <button type="button" className="ghost-button" onClick={() => openRescheduleForm(item)} disabled={actionStatus.saving}>
                            <CalendarDays size={16} />
                            Reschedule
                          </button>
                          <button type="button" className="ghost-button danger-text" onClick={() => onCancelFollowUp(item.id, { version: item.version })} disabled={actionStatus.saving}>
                            <X size={16} />
                            Cancel
                          </button>
                          <button type="button" className="ghost-button" onClick={() => onCompleteFollowUp(item.id, { version: item.version })} disabled={actionStatus.saving}>
                            <CheckCircle2 size={16} />
                            Complete
                          </button>
                        </>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </section>
            <section className="drawer-section">
              <div className="section-heading">
                <h3>Activity Timeline</h3>
                <span>{lead.activities.length}</span>
              </div>
              <div className="timeline">
                {lead.activities.length === 0 && <p className="muted-text">No activity has been recorded yet.</p>}
                {groupActivitiesByDate(lead.activities).map((group) => (
                  <div className="timeline-group" key={group.dateKey}>
                    <h4>{group.label}</h4>
                    {group.items.map((activity) => (
                      <div className="timeline-item" key={activity.id}>
                        <span className={`timeline-dot ${activity.type.toLowerCase()}`} />
                        <div>
                          <strong>{formatActivityType(activity.type)}</strong>
                          <p>{activity.description}</p>
                          <small>{formatTime(activity.createdAt)} - {activity.createdBy}</small>
                        </div>
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </section>
          </div>
        )}
      </aside>
    </div>
  );
}

function InfoItem({ label, value }) {
  return (
    <div className="info-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function Field({ label, error, children, className = "", required = false }) {
  return (
    <label className={className}>
      <span className="field-label-text">
        {label}
        {required && <span className="required-mark" aria-label="required">*</span>}
      </span>
      {children}
      {error && <small className="field-error">{error}</small>}
    </label>
  );
}

function PipelinePage({ pipeline, loading, error, onRetry, onNewLead, onOpenLead, canManageLeads }) {
  return (
    <>
      <PageTitle
        title="Lead Pipeline"
        subtitle="Manage and track student enrollment progress."
        action={
          <button className="primary-button" onClick={onNewLead} disabled={!canManageLeads}>
            <Plus size={18} />
            Create New Lead
          </button>
        }
      />
      <div className="pipeline-toolbar">
        <button className="segmented active">Board</button>
        <button className="segmented">List</button>
        <button className="soft-button">High Priority</button>
        <button className="soft-button">Due Today</button>
      </div>
      {loading && <StatePanel title="Loading pipeline" message="Fetching live stages from CounselMate API..." />}
      {error && <StatePanel title="Could not load pipeline" message={error} action={onRetry} />}
      {!loading && !error && pipeline.length === 0 && <StatePanel title="No pipeline stages" message="No lead stages are configured for this tenant." />}
      {!loading && !error && pipeline.length > 0 && (
        <div className="kanban">
          {pipeline.map((stage) => (
            <section className="kanban-column" key={stage.name}>
              <header>
                <h3>{stage.name}</h3>
                <span>{stage.count}</span>
              </header>
              {stage.leads.map((lead) => (
                <article className="lead-card" key={lead.id} onClick={() => onOpenLead(lead.id)} role="button" tabIndex={0} onKeyDown={(event) => event.key === "Enter" && onOpenLead(lead.id)}>
                  <div className="lead-card-top">
                    <Badge label={(lead.course || "Course").split(" ")[0]} />
                    <MoreVertical size={18} />
                  </div>
                  <h4>{lead.studentName}</h4>
                  <p>{lead.course}</p>
                  <footer>
                    <span className={lead.priority === "High" ? "danger-text" : ""}>{formatFollowUpLabel(lead.nextFollowUpAt)}</span>
                    <span className="mini-avatar">{initials(lead.studentName)}</span>
                  </footer>
                </article>
              ))}
              <button className="add-card">
                <Plus size={18} />
                Add Card
              </button>
            </section>
          ))}
        </div>
      )}
    </>
  );
}

function FollowUpsPage({ followUps, loading, error, onRetry }) {
  return (
    <>
      <PageTitle title="Follow-ups" subtitle="Manage your daily student engagement pipeline." />
      <div className="two-column">
        <div>
          <div className="tabs">
            <button className="active">Today <span>{followUps.length}</span></button>
            <button>Upcoming</button>
            <button>Overdue</button>
          </div>
          <div className="followup-list">
            {loading && <StatePanel title="Loading follow-ups" message="Fetching live scheduled tasks..." />}
            {error && <StatePanel title="Could not load follow-ups" message={error} action={onRetry} />}
            {!loading && !error && followUps.length === 0 && <StatePanel title="No follow-ups" message="No scheduled follow-ups were found." />}
            {!loading && !error && followUps.map((item) => (
              <FollowUpRow key={item.id} item={item} />
            ))}
            <button className="empty-dropzone">
              <Plus size={22} />
              Schedule another follow-up for today
            </button>
          </div>
        </div>
        <aside className="right-rail">
          <Card title="Date Navigator">
            <div className="calendar-card">
              <h3>October 2026</h3>
              <div className="calendar-grid">
                {Array.from({ length: 28 }, (_, index) => (
                  <span key={index} className={index === 17 ? "selected" : ""}>{index + 1}</span>
                ))}
              </div>
            </div>
          </Card>
          <div className="mini-metrics">
            <Metric title="Conversion" value="24%" />
            <Metric title="Avg Response" value="1.2h" />
          </div>
        </aside>
      </div>
    </>
  );
}

function CounselorsPage({
  users,
  branches,
  currentUser,
  loading,
  error,
  saving,
  fieldErrors,
  message,
  canManageUsers,
  onRetry,
  onClearStatus,
  onCreateUser,
  onUpdateUser,
  onResetPassword,
}) {
  const [createOpen, setCreateOpen] = useState(false);
  const [editingUser, setEditingUser] = useState(null);
  const [resettingUser, setResettingUser] = useState(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [branchFilter, setBranchFilter] = useState("all");

  const roleOptions = useMemo(
    () => [...new Set(users.map((user) => user.role))].sort((left, right) => formatRole(left).localeCompare(formatRole(right))),
    [users],
  );
  const branchOptions = useMemo(
    () => [...new Map(users.filter((user) => user.branchId).map((user) => [user.branchId, user.branch || "Unnamed branch"])).entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((left, right) => left.name.localeCompare(right.name)),
    [users],
  );
  const filteredUsers = useMemo(() => {
    const query = searchQuery.trim().toLocaleLowerCase();
    return users.filter((user) => {
      const matchesQuery = !query || `${user.fullName} ${user.email}`.toLocaleLowerCase().includes(query);
      const matchesRole = roleFilter === "all" || user.role === roleFilter;
      const matchesStatus = statusFilter === "all" || (statusFilter === "active" ? user.isActive : !user.isActive);
      const matchesBranch = branchFilter === "all"
        || (branchFilter === "unassigned" ? !user.branchId : user.branchId === branchFilter);
      return matchesQuery && matchesRole && matchesStatus && matchesBranch;
    });
  }, [branchFilter, roleFilter, searchQuery, statusFilter, users]);

  const activeUsers = users.filter((user) => user.isActive).length;
  const inactiveUsers = users.length - activeUsers;
  const administrators = users.filter((user) => user.isActive && ["Owner", "Admin"].includes(user.role)).length;
  const filtersApplied = Boolean(searchQuery.trim()) || roleFilter !== "all" || statusFilter !== "all" || branchFilter !== "all";

  const clearFilters = () => {
    setSearchQuery("");
    setRoleFilter("all");
    setStatusFilter("all");
    setBranchFilter("all");
  };

  const openCreate = () => {
    onClearStatus();
    setEditingUser(null);
    setResettingUser(null);
    setCreateOpen(true);
  };

  const openEdit = (user) => {
    onClearStatus();
    setCreateOpen(false);
    setResettingUser(null);
    setEditingUser(user);
  };

  const openReset = (user) => {
    onClearStatus();
    setCreateOpen(false);
    setEditingUser(null);
    setResettingUser(user);
  };

  return (
    <>
      <PageTitle
        title="Team Management"
        subtitle="Manage tenant admins, counsellors, callers, accountants, and read-only users."
        action={canManageUsers ? (
          <button className="primary-button" onClick={openCreate}>
            <UserPlus size={18} />
            Add User
          </button>
        ) : null}
      />

      {!loading && users.length > 0 && (
        <div className="team-summary" aria-label="Team summary">
          <div><Users size={20} /><span>Total users</span><strong>{users.length}</strong></div>
          <div><UserCheck size={20} /><span>Active</span><strong>{activeUsers}</strong></div>
          <div><UserX size={20} /><span>Inactive</span><strong>{inactiveUsers}</strong></div>
          <div><KeyRound size={20} /><span>Admins</span><strong>{administrators}</strong></div>
        </div>
      )}

      {message && (
        <div className="team-notice success" role="status">
          <CheckCircle2 size={19} />
          <span>{message}</span>
          <button className="icon-button" type="button" onClick={onClearStatus} aria-label="Dismiss message" title="Dismiss">
            <X size={18} />
          </button>
        </div>
      )}

      {error && users.length > 0 && !createOpen && !editingUser && !resettingUser && (
        <div className="team-notice error" role="alert">
          <span>{error}</span>
          <button className="soft-button" type="button" onClick={onRetry}><RotateCcw size={17} />Retry</button>
        </div>
      )}

      {createOpen && (
        <UserFormModal
          title="Add User"
          branches={branches}
          currentUser={currentUser}
          saving={saving}
          error={error}
          fieldErrors={fieldErrors}
          submitLabel="Create User"
          onCancel={() => {
            setCreateOpen(false);
            onClearStatus();
          }}
          onSubmit={async (payload) => {
            const created = await onCreateUser(payload);
            if (created) {
              setCreateOpen(false);
            }
          }}
        />
      )}

      {editingUser && (
        <UserFormModal
          title={`Edit ${editingUser.fullName}`}
          user={editingUser}
          branches={branches}
          currentUser={currentUser}
          saving={saving}
          error={error}
          fieldErrors={fieldErrors}
          submitLabel="Save User"
          onCancel={() => {
            setEditingUser(null);
            onClearStatus();
          }}
          onSubmit={async (payload) => {
            const updated = await onUpdateUser(editingUser.id, payload);
            if (updated) {
              setEditingUser(null);
            }
          }}
        />
      )}

      {resettingUser && (
        <ResetPasswordPanel
          user={resettingUser}
          saving={saving}
          error={error}
          fieldErrors={fieldErrors}
          onCancel={() => {
            setResettingUser(null);
            onClearStatus();
          }}
          onSubmit={async (payload) => {
            const updated = await onResetPassword(resettingUser.id, payload);
            if (updated) {
              setResettingUser(null);
            }
          }}
        />
      )}

      {!loading && users.length > 0 && (
        <div className="team-toolbar">
          <label className="team-search">
            <span className="sr-only">Search users</span>
            <Search size={18} />
            <input
              type="search"
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
              placeholder="Search name or email"
            />
          </label>
          <label>
            <span>Role</span>
            <select value={roleFilter} onChange={(event) => setRoleFilter(event.target.value)}>
              <option value="all">All roles</option>
              {roleOptions.map((role) => <option key={role} value={role}>{formatRole(role)}</option>)}
            </select>
          </label>
          <label>
            <span>Status</span>
            <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </label>
          <label>
            <span>Branch</span>
            <select value={branchFilter} onChange={(event) => setBranchFilter(event.target.value)}>
              <option value="all">All branches</option>
              <option value="unassigned">No branch</option>
              {branchOptions.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </label>
          {filtersApplied && (
            <button className="ghost-button team-clear-filter" type="button" onClick={clearFilters}>
              <X size={17} />Clear
            </button>
          )}
        </div>
      )}

      <div className="table-card">
        {loading && <StatePanel title="Loading users" message="Fetching tenant team members..." />}
        {error && users.length === 0 && !createOpen && !editingUser && !resettingUser && <StatePanel title="Could not load users" message={error} action={onRetry} />}
        {!loading && !error && users.length === 0 && <StatePanel title="No users" message="No users were found for this tenant." />}
        {!loading && users.length > 0 && filteredUsers.length === 0 && (
          <StatePanel title="No matching users" message="Try changing or clearing the directory filters." action={clearFilters} />
        )}
        {!loading && filteredUsers.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>User</th>
                <th>Role</th>
                <th>Branch</th>
                <th>Status</th>
                <th>Last Login</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.map((user) => {
                const editable = canEditUser(currentUser, user, canManageUsers);
                const resettable = canResetUserPassword(currentUser, user, canManageUsers);
                return <tr key={user.id}>
                  <td>
                    <div className="student-cell">
                      <span>{initials(user.fullName)}</span>
                      <div>
                        <strong>{user.fullName}</strong>
                        <small>{user.email}</small>
                      </div>
                    </div>
                  </td>
                  <td><Badge label={formatRole(user.role)} muted={user.role === "ReadOnly"} /></td>
                  <td>{user.branch || "No branch"}</td>
                  <td><Badge label={user.isActive ? "Active" : "Inactive"} muted={!user.isActive} /></td>
                  <td>{user.lastLoginAt ? `${formatDate(user.lastLoginAt)}, ${formatTime(user.lastLoginAt)}` : "Never"}</td>
                  <td>
                    <div className="table-actions team-actions">
                      {editable && (
                        <button className="icon-button table-action-button" type="button" onClick={() => openEdit(user)} aria-label={`Edit ${user.fullName}`} title="Edit user">
                          <Pencil size={17} />
                        </button>
                      )}
                      {resettable && (
                        <button className="icon-button table-action-button" type="button" onClick={() => openReset(user)} aria-label={`Reset password for ${user.fullName}`} title="Reset password">
                          <KeyRound size={17} />
                        </button>
                      )}
                      {!editable && !resettable && (
                        <span className="team-access-label">{user.id === currentUser.userId ? "You" : "View only"}</span>
                      )}
                    </div>
                  </td>
                </tr>
              })}
            </tbody>
          </table>
        )}
        {!loading && users.length > 0 && (
          <footer className="table-footer team-table-footer">
            <span>Showing {filteredUsers.length} of {users.length} users</span>
          </footer>
        )}
      </div>
    </>
  );
}

function UserFormModal({ title, user, branches, currentUser, saving, error, fieldErrors, submitLabel, onCancel, onSubmit }) {
  const [form, setForm] = useState(() => ({
    fullName: user?.fullName || "",
    email: user?.email || "",
    role: user?.role || "Counselor",
    branchId: user?.branchId || "",
    isActive: user?.isActive ?? true,
    password: "",
  }));
  const [clientErrors, setClientErrors] = useState({});
  const [passwordVisible, setPasswordVisible] = useState(false);
  const isEditing = Boolean(user);
  const isCurrentUser = user?.id === currentUser?.userId;

  useEffect(() => {
    setForm({
      fullName: user?.fullName || "",
      email: user?.email || "",
      role: user?.role || "Counselor",
      branchId: user?.branchId || "",
      isActive: user?.isActive ?? true,
      password: "",
    });
    setClientErrors({});
    setPasswordVisible(false);
  }, [user]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === "Escape" && !saving) {
        onCancel();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onCancel, saving]);

  const roleOptions = getManagedRoleOptions(currentUser);
  const getFieldError = (field) => clientErrors[field] || firstError(fieldErrors[field]);

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setClientErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const validationErrors = validateUserForm(form, isEditing);
    setClientErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    const payload = {
      fullName: form.fullName.trim(),
      role: form.role,
      branchId: optionalValue(form.branchId),
    };

    if (isEditing) {
      payload.isActive = form.isActive;
    } else {
      payload.email = form.email.trim();
      payload.password = form.password;
    }

    onSubmit(payload);
  };

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && !saving && onCancel()}>
      <form className="modal team-modal" onSubmit={handleSubmit} noValidate role="dialog" aria-modal="true" aria-labelledby="team-user-modal-title">
        <header className="modal-header">
          <div>
            <h2 id="team-user-modal-title">{title}</h2>
            <p>{isEditing ? "Update this user's role, branch, and account status." : "Create a tenant-scoped user account."}</p>
          </div>
          <button type="button" className="icon-button modal-close" onClick={onCancel} disabled={saving} aria-label="Close" title="Close">
            <X size={20} />
          </button>
        </header>
        <div className="team-modal-body">
          {error && <div className="form-alert" role="alert">{error}</div>}
          <div className="form-grid">
        <Field label="Full Name" error={getFieldError("fullName")} required>
          <input value={form.fullName} maxLength={160} onChange={(event) => updateField("fullName", event.target.value)} required autoFocus />
        </Field>
        <Field label="Email" error={getFieldError("email")} required>
          <input value={form.email} type="email" maxLength={240} onChange={(event) => updateField("email", event.target.value)} disabled={isEditing} required />
        </Field>
        <Field label="Role" error={getFieldError("role")} required>
          <select value={form.role} onChange={(event) => updateField("role", event.target.value)} disabled={isCurrentUser} required>
            {roleOptions.map((item) => <option key={item} value={item}>{formatRole(item)}</option>)}
          </select>
        </Field>
        <Field label="Branch" error={getFieldError("branchId")}>
          <select value={form.branchId} onChange={(event) => updateField("branchId", event.target.value)}>
            <option value="">No branch</option>
            {branches.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        {isEditing && (
          <Field label="Status" error={getFieldError("isActive")}>
            <select value={form.isActive ? "active" : "inactive"} onChange={(event) => updateField("isActive", event.target.value === "active")} disabled={isCurrentUser}>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </Field>
        )}
        {!isEditing && (
          <PasswordField
            label="Initial Password"
            value={form.password}
            visible={passwordVisible}
            error={getFieldError("password")}
            onToggle={() => setPasswordVisible((current) => !current)}
            onChange={(value) => updateField("password", value)}
            className="span-2"
          />
        )}
          </div>
          {!isEditing && <p className="password-policy">Use 8-120 characters with uppercase, lowercase, a number, and a special character.</p>}
        </div>
        <footer className="team-modal-actions">
          <button type="button" className="ghost-button" onClick={onCancel} disabled={saving}>Cancel</button>
          <button type="submit" className="primary-button" disabled={saving}>{saving ? "Saving..." : submitLabel}</button>
        </footer>
      </form>
    </div>
  );
}

function ResetPasswordPanel({ user, saving, error, fieldErrors, onCancel, onSubmit }) {
  const [form, setForm] = useState({
    newPassword: "",
    confirmPassword: "",
  });
  const [clientErrors, setClientErrors] = useState({});
  const [visible, setVisible] = useState({
    newPassword: false,
    confirmPassword: false,
  });
  const getFieldError = (field) => clientErrors[field] || firstError(fieldErrors[field]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === "Escape" && !saving) {
        onCancel();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onCancel, saving]);

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setClientErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const validationErrors = validatePasswordResetForm(form);
    setClientErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    onSubmit(form);
  };

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && !saving && onCancel()}>
      <form className="modal team-modal password-modal" onSubmit={handleSubmit} noValidate role="dialog" aria-modal="true" aria-labelledby="reset-user-password-title">
        <header className="modal-header">
          <div>
            <h2 id="reset-user-password-title">Reset Password</h2>
            <p>{user.fullName} | {user.email}</p>
          </div>
          <button type="button" className="icon-button modal-close" onClick={onCancel} disabled={saving} aria-label="Close" title="Close">
            <X size={20} />
          </button>
        </header>
        <div className="team-modal-body">
          {error && <div className="form-alert" role="alert">{error}</div>}
          <p className="reset-warning">This immediately replaces the user's current password and clears failed login attempts.</p>
          <div className="form-grid single-column">
            <PasswordField
              label="New Password"
              value={form.newPassword}
              visible={visible.newPassword}
              error={getFieldError("newPassword")}
              onToggle={() => setVisible((current) => ({ ...current, newPassword: !current.newPassword }))}
              onChange={(value) => updateField("newPassword", value)}
              autoFocus
            />
            <PasswordField
              label="Confirm Password"
              value={form.confirmPassword}
              visible={visible.confirmPassword}
              error={getFieldError("confirmPassword")}
              onToggle={() => setVisible((current) => ({ ...current, confirmPassword: !current.confirmPassword }))}
              onChange={(value) => updateField("confirmPassword", value)}
            />
          </div>
          <p className="password-policy">Use 8-120 characters with uppercase, lowercase, a number, and a special character.</p>
        </div>
        <footer className="team-modal-actions">
          <button type="button" className="ghost-button" onClick={onCancel} disabled={saving}>Cancel</button>
          <button type="submit" className="primary-button" disabled={saving}>{saving ? "Resetting..." : "Reset Password"}</button>
        </footer>
      </form>
    </div>
  );
}

function ReportsPage() {
  return (
    <>
      <PageTitle
        title="Admissions Reports"
        subtitle="Real-time performance metrics and conversion insights."
        action={
          <button className="primary-button">
            <Download size={18} />
            Export Report
          </button>
        }
      />
      <div className="reports-grid">
        <div className="metric-stack">
          <Metric title="Total Inquiries" value="2,840" trend="+12.5%" />
          <Metric title="Conversion Rate" value="18.4%" trend="+4.2%" />
          <Metric title="Avg. Acquisition Cost" value="Rs. 142" trend="-2.1%" warning />
        </div>
        <Card title="Lead Conversion Funnel" className="funnel-card">
          <div className="funnel">
            {["Total Inquiries", "Qualified Leads", "Applications", "Offers Made", "Enrolled"].map((label, index) => (
              <div key={label} style={{ width: `${100 - index * 12}%` }}>
                <strong>{label}</strong>
                <span>{[2840, 1920, 850, 620, 524][index]}</span>
              </div>
            ))}
          </div>
        </Card>
        <Card title="Lead Source">
          <SourceBreakdown />
        </Card>
        <Card title="Counsellor Productivity">
          {counselors.map((person) => (
            <div className="progress-row" key={person.name}>
              <span>{person.name}</span>
              <strong>{person.leads} Leads</strong>
              <div><span style={{ width: `${Math.min(person.leads / 1.5, 100)}%` }} /></div>
            </div>
          ))}
        </Card>
      </div>
    </>
  );
}

const masterDataTabs = [
  { id: "branches", label: "Branches", singular: "Branch", apiPath: "branches", icon: Building2 },
  { id: "courses", label: "Courses", singular: "Course", apiPath: "courses", icon: BookOpen },
  { id: "sources", label: "Lead Sources", singular: "Lead Source", apiPath: "lead-sources", icon: Radio },
  { id: "stages", label: "Pipeline Stages", singular: "Lead Stage", apiPath: "lead-stages", icon: GitBranch },
];

function SettingsPage({ currentUser, onMasterDataChanged }) {
  const [masterData, setMasterData] = useState({ branches: [], courses: [], sources: [], stages: [] });
  const [activeTab, setActiveTab] = useState("branches");
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [editor, setEditor] = useState(null);
  const [status, setStatus] = useState({ loading: true, saving: false, error: "", fieldErrors: {}, message: "" });
  const canManage = ["Owner", "Admin"].includes(currentUser.role);
  const tab = masterDataTabs.find((item) => item.id === activeTab) || masterDataTabs[0];

  const loadMasterData = useCallback(async (silent = false) => {
    if (!silent) {
      setStatus((current) => ({ ...current, loading: true, error: "", message: "" }));
    }
    try {
      const response = await getMasterData();
      setMasterData(response);
      setStatus((current) => ({ ...current, loading: false, error: "" }));
      return true;
    } catch (error) {
      setStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load master data.",
      }));
      return false;
    }
  }, []);

  useEffect(() => {
    loadMasterData();
  }, [loadMasterData]);

  const records = masterData[activeTab] || [];
  const visibleRecords = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    return records.filter((record) => {
      const searchText = `${record.name} ${record.city || ""}`.toLocaleLowerCase();
      const matchesQuery = !normalizedQuery || searchText.includes(normalizedQuery);
      const matchesStatus = statusFilter === "all" || (statusFilter === "active" ? record.isActive : !record.isActive);
      return matchesQuery && matchesStatus;
    });
  }, [query, records, statusFilter]);

  const clearActionStatus = () => {
    setStatus((current) => ({ ...current, error: "", fieldErrors: {}, message: "" }));
  };

  const openEditor = (record = null) => {
    clearActionStatus();
    setEditor({ type: activeTab, record });
  };

  const saveRecord = async (type, record, payload) => {
    const config = masterDataTabs.find((item) => item.id === type);
    setStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {}, message: "" }));
    try {
      const response = record
        ? await updateMasterRecord(config.apiPath, record.id, payload)
        : await createMasterRecord(config.apiPath, payload);
      const refreshed = await getMasterData();
      setMasterData(refreshed);
      setEditor(null);
      setStatus({
        loading: false,
        saving: false,
        error: "",
        fieldErrors: {},
        message: response.message || `${config.singular} saved.`,
      });
      onMasterDataChanged?.();
      return true;
    } catch (error) {
      setStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : `Unable to save ${config.singular.toLowerCase()}.`,
        fieldErrors: error?.errors || {},
        message: "",
      }));
      return false;
    }
  };

  const moveStage = async (stage, direction) => {
    const activeStages = masterData.stages.filter((item) => item.isActive).sort((left, right) => left.sortOrder - right.sortOrder);
    const currentIndex = activeStages.findIndex((item) => item.id === stage.id);
    const targetIndex = currentIndex + direction;
    if (currentIndex < 0 || targetIndex < 0 || targetIndex >= activeStages.length) {
      return;
    }
    const reordered = [...activeStages];
    [reordered[currentIndex], reordered[targetIndex]] = [reordered[targetIndex], reordered[currentIndex]];
    setStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {}, message: "" }));
    try {
      const response = await reorderLeadStages(reordered.map((item) => ({ id: item.id, version: item.version })));
      const refreshed = await getMasterData();
      setMasterData(refreshed);
      setStatus({ loading: false, saving: false, error: "", fieldErrors: {}, message: response.message || "Lead stages reordered." });
      onMasterDataChanged?.();
    } catch (error) {
      setStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to reorder lead stages.",
        fieldErrors: error?.errors || {},
        message: "",
      }));
    }
  };

  const activeCount = records.filter((record) => record.isActive).length;

  return (
    <>
      <PageTitle
        title="Master Data"
        subtitle="Configure the branches, courses, sources, and stages used across this CRM workspace."
        action={canManage ? (
          <button className="primary-button" type="button" onClick={() => openEditor()} disabled={status.loading || status.saving}>
            <Plus size={18} />Add {tab.singular}
          </button>
        ) : null}
      />

      <nav className="master-tabs" aria-label="Master data categories">
        {masterDataTabs.map((item) => {
          const Icon = item.icon;
          const itemRecords = masterData[item.id] || [];
          return (
            <button
              key={item.id}
              type="button"
              className={activeTab === item.id ? "active" : ""}
              onClick={() => {
                setActiveTab(item.id);
                setQuery("");
                setStatusFilter("all");
                clearActionStatus();
              }}
            >
              <Icon size={18} />
              <span>{item.label}</span>
              <strong>{itemRecords.length}</strong>
            </button>
          );
        })}
      </nav>

      {status.message && (
        <div className="team-notice success" role="status">
          <CheckCircle2 size={19} /><span>{status.message}</span>
          <button className="icon-button" type="button" onClick={clearActionStatus} aria-label="Dismiss message"><X size={18} /></button>
        </div>
      )}
      {status.error && !editor && (
        <div className="team-notice error" role="alert">
          <span>{status.error}</span>
          <button className="soft-button" type="button" onClick={() => loadMasterData()}><RotateCcw size={17} />Retry</button>
        </div>
      )}

      {!status.loading && (
        <div className="master-summary">
          <span><strong>{records.length}</strong> total</span>
          <span><strong>{activeCount}</strong> active</span>
          <span><strong>{records.length - activeCount}</strong> inactive</span>
          {!canManage && <span className="read-only-note">Read-only access</span>}
        </div>
      )}

      {!status.loading && records.length > 0 && (
        <div className="master-toolbar">
          <label className="team-search">
            <span className="sr-only">Search {tab.label.toLowerCase()}</span>
            <Search size={18} />
            <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder={`Search ${tab.label.toLowerCase()}`} />
          </label>
          <label>
            <span>Status</span>
            <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </label>
          {(query || statusFilter !== "all") && (
            <button className="ghost-button" type="button" onClick={() => { setQuery(""); setStatusFilter("all"); }}>
              <X size={17} />Clear
            </button>
          )}
        </div>
      )}

      <div className="table-card master-table">
        {status.loading && <StatePanel title="Loading master data" message="Fetching tenant configuration..." />}
        {!status.loading && records.length === 0 && <StatePanel title={`No ${tab.label.toLowerCase()}`} message={`Add the first ${tab.singular.toLowerCase()} to this workspace.`} />}
        {!status.loading && records.length > 0 && visibleRecords.length === 0 && (
          <StatePanel title="No matching records" message="Change or clear the current filters." action={() => { setQuery(""); setStatusFilter("all"); }} />
        )}
        {!status.loading && visibleRecords.length > 0 && (
          <table>
            <thead>
              <tr>
                {activeTab === "stages" && <th>Order</th>}
                <th>{tab.singular}</th>
                {activeTab === "branches" && <th>City</th>}
                <th>Usage</th>
                {activeTab === "stages" && <th>Type</th>}
                <th>Status</th>
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleRecords.map((record) => {
                const activeStages = masterData.stages.filter((item) => item.isActive).sort((left, right) => left.sortOrder - right.sortOrder);
                const stageIndex = activeTab === "stages" ? activeStages.findIndex((item) => item.id === record.id) : -1;
                return (
                  <tr key={record.id}>
                    {activeTab === "stages" && (
                      <td>
                        {canManage && record.isActive ? (
                          <div className="stage-order-actions">
                            <button className="icon-button table-action-button" type="button" onClick={() => moveStage(record, -1)} disabled={status.saving || stageIndex <= 0} aria-label={`Move ${record.name} up`} title="Move up"><ArrowUp size={16} /></button>
                            <button className="icon-button table-action-button" type="button" onClick={() => moveStage(record, 1)} disabled={status.saving || stageIndex < 0 || stageIndex >= activeStages.length - 1} aria-label={`Move ${record.name} down`} title="Move down"><ArrowDown size={16} /></button>
                          </div>
                        ) : <span className="team-access-label">{record.isActive ? stageIndex + 1 : "-"}</span>}
                      </td>
                    )}
                    <td><strong>{record.name}</strong></td>
                    {activeTab === "branches" && <td>{record.city}</td>}
                    <td>{activeTab === "branches" ? `${record.activeUsers} users / ${record.leads} leads` : `${record.leads} leads`}</td>
                    {activeTab === "stages" && (
                      <td><StageTypeBadges stage={record} /></td>
                    )}
                    <td><Badge label={record.isActive ? "Active" : "Inactive"} muted={!record.isActive} /></td>
                    <td>{formatDate(record.updatedAt)}</td>
                    <td>
                      <div className="table-actions team-actions">
                        {canManage ? (
                          <button className="icon-button table-action-button" type="button" onClick={() => openEditor(record)} aria-label={`Edit ${record.name}`} title="Edit">
                            <Pencil size={17} />
                          </button>
                        ) : <span className="team-access-label">View only</span>}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
        {!status.loading && records.length > 0 && <footer className="table-footer team-table-footer">Showing {visibleRecords.length} of {records.length} records</footer>}
      </div>

      {editor && (
        <MasterDataModal
          key={`${editor.type}-${editor.record?.id || "new"}`}
          type={editor.type}
          record={editor.record}
          saving={status.saving}
          error={status.error}
          fieldErrors={status.fieldErrors}
          onClose={() => { if (!status.saving) { setEditor(null); clearActionStatus(); } }}
          onSubmit={(payload) => saveRecord(editor.type, editor.record, payload)}
        />
      )}
    </>
  );
}

function StageTypeBadges({ stage }) {
  return (
    <div className="stage-type-badges">
      {stage.isDefaultStage && <Badge label="Default" />}
      {stage.isWonStage && <Badge label="Won" />}
      {stage.isLostStage && <Badge label="Lost" danger />}
      {!stage.isDefaultStage && !stage.isWonStage && !stage.isLostStage && <span className="muted-text">Standard</span>}
    </div>
  );
}

function MasterDataModal({ type, record, saving, error, fieldErrors, onClose, onSubmit }) {
  const config = masterDataTabs.find((item) => item.id === type);
  const isBranch = type === "branches";
  const isStage = type === "stages";
  const isEditing = Boolean(record);
  const [form, setForm] = useState({
    name: record?.name || "",
    city: record?.city || "",
    isActive: record?.isActive ?? true,
    isDefaultStage: record?.isDefaultStage ?? false,
    isWonStage: record?.isWonStage ?? false,
    isLostStage: record?.isLostStage ?? false,
  });
  const [clientErrors, setClientErrors] = useState({});
  const getFieldError = (field) => clientErrors[field] || firstError(fieldErrors[field]);

  useEffect(() => {
    const handleKeyDown = (event) => event.key === "Escape" && !saving && onClose();
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose, saving]);

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setClientErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const updateStageFlag = (field, checked) => {
    setForm((current) => {
      const next = { ...current, [field]: checked };
      if (checked && field === "isDefaultStage") {
        next.isWonStage = false;
        next.isLostStage = false;
      } else if (checked && field === "isWonStage") {
        next.isDefaultStage = false;
        next.isLostStage = false;
      } else if (checked && field === "isLostStage") {
        next.isDefaultStage = false;
        next.isWonStage = false;
      }
      return next;
    });
    setClientErrors({});
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const errors = {};
    const maxNameLength = isBranch || type === "courses" ? 160 : 120;
    if (!form.name.trim()) errors.name = "Name is required.";
    else if (form.name.trim().length > maxNameLength) errors.name = `Name must be ${maxNameLength} characters or fewer.`;
    if (isBranch && !form.city.trim()) errors.city = "City is required.";
    else if (isBranch && form.city.trim().length > 120) errors.city = "City must be 120 characters or fewer.";
    setClientErrors(errors);
    if (Object.keys(errors).length > 0) return;

    const payload = { name: form.name.trim() };
    if (isBranch) payload.city = form.city.trim();
    if (isStage) {
      payload.isDefaultStage = form.isDefaultStage;
      payload.isWonStage = form.isWonStage;
      payload.isLostStage = form.isLostStage;
    }
    if (isEditing) {
      payload.isActive = form.isActive;
      payload.version = record.version;
    }
    onSubmit(payload);
  };

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && !saving && onClose()}>
      <form className="modal team-modal master-modal" onSubmit={handleSubmit} noValidate role="dialog" aria-modal="true" aria-labelledby="master-modal-title">
        <header className="modal-header">
          <div>
            <h2 id="master-modal-title">{isEditing ? "Edit" : "Add"} {config.singular}</h2>
            <p>{isEditing ? "Update this tenant configuration record." : `Create a new ${config.singular.toLowerCase()} for this workspace.`}</p>
          </div>
          <button type="button" className="icon-button modal-close" onClick={onClose} disabled={saving} aria-label="Close"><X size={20} /></button>
        </header>
        <div className="team-modal-body">
          {error && <div className="form-alert" role="alert">{error}</div>}
          <div className="form-grid">
            <Field label="Name" error={getFieldError("name")} className={isBranch ? "" : "span-2"} required>
              <input value={form.name} maxLength={isBranch || type === "courses" ? 160 : 120} onChange={(event) => updateField("name", event.target.value)} autoFocus required />
            </Field>
            {isBranch && (
              <Field label="City" error={getFieldError("city")} required>
                <input value={form.city} maxLength={120} onChange={(event) => updateField("city", event.target.value)} required />
              </Field>
            )}
          </div>

          {isStage && (
            <fieldset className="master-options">
              <legend>Stage behavior</legend>
              <label className="master-checkbox"><input type="checkbox" checked={form.isDefaultStage} onChange={(event) => updateStageFlag("isDefaultStage", event.target.checked)} />Default stage</label>
              <label className="master-checkbox"><input type="checkbox" checked={form.isWonStage} onChange={(event) => updateStageFlag("isWonStage", event.target.checked)} />Won outcome</label>
              <label className="master-checkbox"><input type="checkbox" checked={form.isLostStage} onChange={(event) => updateStageFlag("isLostStage", event.target.checked)} />Lost outcome</label>
              {getFieldError("stageType") && <small className="field-error">{getFieldError("stageType")}</small>}
              {getFieldError("isDefaultStage") && <small className="field-error">{getFieldError("isDefaultStage")}</small>}
            </fieldset>
          )}

          {isEditing && (
            <fieldset className="master-options status-option">
              <legend>Availability</legend>
              <label className="master-checkbox">
                <input type="checkbox" checked={form.isActive} onChange={(event) => updateField("isActive", event.target.checked)} />
                Active and available for new records
              </label>
            </fieldset>
          )}

          {isEditing && record.isActive && !form.isActive && (
            <p className="reset-warning">This record will remain on historical leads but will no longer be available for new selections. Dependency rules are checked when you save.</p>
          )}
        </div>
        <footer className="team-modal-actions">
          <button type="button" className="ghost-button" onClick={onClose} disabled={saving}>Cancel</button>
          <button type="submit" className="primary-button" disabled={saving}>{saving ? "Saving..." : isEditing ? "Save Changes" : `Add ${config.singular}`}</button>
        </footer>
      </form>
    </div>
  );
}

function Metric({ title, value, trend, warning }) {
  return (
    <div className="metric-card">
      <span className="metric-icon" />
      {trend && <em className={warning ? "warning" : ""}>{trend}</em>}
      <p>{title}</p>
      <strong>{value}</strong>
    </div>
  );
}

function Card({ title, badge, children, className = "" }) {
  return (
    <section className={`card ${className}`}>
      <header className="card-header">
        <h2>{title}</h2>
        {badge && <span className="card-badge">{badge}</span>}
      </header>
      <div className="card-body">{children}</div>
    </section>
  );
}

function FilterBar({ filters, options, total, loading, onChange, onReset }) {
  const [searchDraft, setSearchDraft] = useState(filters.search || "");

  useEffect(() => {
    setSearchDraft(filters.search || "");
  }, [filters.search]);

  const submitSearch = () => {
    onChange({ search: searchDraft.trim() });
  };

  return (
    <div className="lead-filter-panel">
      <label className="team-search">
        <span>Search</span>
        <input
          value={searchDraft}
          placeholder="Name, phone, email, lead ID"
          onChange={(event) => setSearchDraft(event.target.value)}
          onBlur={submitSearch}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              submitSearch();
            }
          }}
        />
      </label>

      <label>
        <span>Stage</span>
        <select value={filters.stageId} onChange={(event) => onChange({ stageId: event.target.value })}>
          <option value="">All stages</option>
          {options.stages.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>

      <label>
        <span>Course</span>
        <select value={filters.courseId} onChange={(event) => onChange({ courseId: event.target.value })}>
          <option value="">All courses</option>
          {options.courses.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>

      <label>
        <span>Source</span>
        <select value={filters.sourceId} onChange={(event) => onChange({ sourceId: event.target.value })}>
          <option value="">All sources</option>
          {options.sources.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>

      <label>
        <span>Branch</span>
        <select value={filters.branchId} onChange={(event) => onChange({ branchId: event.target.value })}>
          <option value="">All branches</option>
          {options.branches.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>

      <label>
        <span>Counsellor</span>
        <select value={filters.assignedUserId} onChange={(event) => onChange({ assignedUserId: event.target.value })}>
          <option value="">Everyone</option>
          {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>

      <label>
        <span>Priority</span>
        <select value={filters.priority} onChange={(event) => onChange({ priority: event.target.value })}>
          <option value="">All priorities</option>
          {["Urgent", "High", "Medium", "Low"].map((item) => <option key={item} value={item}>{item}</option>)}
        </select>
      </label>

      <label>
        <span>Archive</span>
        <select value={filters.archive} onChange={(event) => onChange({ archive: event.target.value })}>
          <option value="active">Active</option>
          <option value="archived">Archived</option>
          <option value="all">All</option>
        </select>
      </label>

      <label>
        <span>Sort</span>
        <select value={filters.sort} onChange={(event) => onChange({ sort: event.target.value })}>
          <option value="newest">Newest</option>
          <option value="oldest">Oldest</option>
          <option value="name">Name</option>
          <option value="follow-up">Follow-up</option>
          <option value="priority">Priority</option>
        </select>
      </label>

      <div className="lead-filter-actions">
        <strong>{formatNumber(total)} leads</strong>
        <button className="soft-button" onClick={onReset} disabled={loading}>Reset</button>
      </div>
    </div>
  );
}

function LeadsTable({ leads, page, pageSize, total, loading, error, onRetry, onOpenLead, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil(total / Math.max(pageSize, 1)));
  const start = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(total, page * pageSize);

  return (
    <div className="table-card">
      {loading && <StatePanel title="Loading leads" message="Fetching live leads from CounselMate API..." />}
      {error && <StatePanel title="Could not load leads" message={error} action={onRetry} />}
      {!loading && !error && leads.length === 0 && <StatePanel title="No leads" message="No leads were found for this tenant." />}
      {!loading && !error && leads.length > 0 && (
      <table>
        <thead>
          <tr>
            <th><input type="checkbox" /></th>
            <th>Student Name</th>
            <th>Phone</th>
            <th>Source</th>
            <th>Course</th>
            <th>Counsellor</th>
            <th>Status</th>
            <th>Stage</th>
            <th>Next Follow-up</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {leads.map((lead) => (
            <tr key={lead.id} className="clickable-row" onClick={() => onOpenLead(lead.id)}>
              <td><input type="checkbox" onClick={(event) => event.stopPropagation()} /></td>
              <td>
                <div className="student-cell">
                  <span>{initials(lead.studentName)}</span>
                  <div>
                    <strong>{lead.studentName}</strong>
                    <small>{lead.email}</small>
                  </div>
                </div>
              </td>
              <td>{lead.phone}</td>
              <td><Badge label={lead.source} muted /></td>
              <td>{lead.course}</td>
              <td>{lead.counselor}</td>
              <td>
                <Status status={lead.archivedAt ? "Archived" : lead.status} />
              </td>
              <td>{lead.stage}</td>
              <td>{formatFollowUpLabel(lead.nextFollowUpAt)}</td>
              <td><MoreVertical size={20} /></td>
            </tr>
          ))}
        </tbody>
      </table>
      )}
      <footer className="table-footer">
        <span>Showing {loading || error ? 0 : start}-{loading || error ? 0 : end} of {loading || error ? 0 : total} leads</span>
        <div>
          <button className="pager" disabled={page <= 1 || loading} onClick={() => onPageChange(page - 1)}>Prev</button>
          <button className="pager active" disabled>{page}</button>
          <button className="pager" disabled={page >= totalPages || loading} onClick={() => onPageChange(page + 1)}>Next</button>
        </div>
      </footer>
    </div>
  );
}

function FollowUpRow({ item, compact = false }) {
  return (
    <article className={`followup-row ${compact ? "compact" : ""}`}>
      <div className="channel-icon">{item.type[0]}</div>
      <div>
        <h3>{item.studentName}</h3>
        <Badge label={`${item.priority} Priority`} danger={item.priority === "High"} warning={item.priority === "Medium"} />
        <p>{item.assignedTo}</p>
      </div>
      <div>
        <small>{item.status}</small>
        <strong>{formatTime(item.dueAt)}</strong>
        <p>{formatDate(item.dueAt)}</p>
      </div>
      {!compact && <button className="primary-button"><CheckCircle2 size={18} />Complete</button>}
    </article>
  );
}

function SourceTable() {
  return (
    <table className="source-table">
      <thead>
        <tr>
          <th>Source Channel</th>
          <th>Leads</th>
          <th>Conversion</th>
          <th>Revenue</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {[
          ["Google Search", 452, "12.5%", "Rs. 14.2L", "Stable"],
          ["Instagram Ads", 318, "8.2%", "Rs. 8.9L", "Growing"],
          ["Referral", 184, "24.1%", "Rs. 9.2L", "Strong"],
        ].map((row) => (
          <tr key={row[0]}>
            {row.map((cell, index) => (
              <td key={cell}>{index === 4 ? <Badge label={cell} /> : cell}</td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function SourceBreakdown() {
  return (
    <div className="source-breakdown">
      {["Google Ads 35%", "Direct Website 25%", "Referrals 20%", "Social Media 20%"].map((item) => (
        <div key={item}><span />{item}</div>
      ))}
    </div>
  );
}

function Badge({ label, muted, danger, warning }) {
  return <span className={`badge ${muted ? "muted" : ""} ${danger ? "danger" : ""} ${warning ? "warning" : ""}`}>{label}</span>;
}

function Status({ status }) {
  const key = status.toLowerCase().replace(/\s/g, "-");
  return <span className={`status ${key}`}>{status}</span>;
}

function initials(name) {
  return (name || "?").split(" ").map((part) => part[0]).join("").slice(0, 2).toUpperCase();
}

function StatePanel({ title, message, action }) {
  return (
    <div className="state-panel">
      <strong>{title}</strong>
      <p>{message}</p>
      {action && <button className="soft-button" onClick={action}>Retry</button>}
    </div>
  );
}

function formatNumber(value) {
  return typeof value === "number" ? new Intl.NumberFormat("en-IN").format(value) : "-";
}

function formatDate(value) {
  if (!value) {
    return "No date";
  }

  return new Intl.DateTimeFormat("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(new Date(value));
}

function formatTime(value) {
  if (!value) {
    return "No time";
  }

  return new Intl.DateTimeFormat("en-IN", {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}

function formatFollowUpLabel(value) {
  if (!value) {
    return "No follow-up";
  }

  return `${formatDate(value)}, ${formatTime(value)}`;
}

function groupActivitiesByDate(activities) {
  const groups = new Map();
  activities.forEach((activity) => {
    const dateKey = new Date(activity.createdAt).toISOString().slice(0, 10);
    if (!groups.has(dateKey)) {
      groups.set(dateKey, {
        dateKey,
        label: formatDate(activity.createdAt),
        items: [],
      });
    }
    groups.get(dateKey).items.push(activity);
  });

  return Array.from(groups.values());
}

function formatActivityType(type) {
  const labels = {
    LeadCreated: "Lead created",
    LeadUpdated: "Lead updated",
    LeadAssigned: "Lead assigned",
    StageChanged: "Stage changed",
    LeadArchived: "Lead archived",
    LeadRestored: "Lead restored",
    FollowUpScheduled: "Follow-up scheduled",
    FollowUpRescheduled: "Follow-up rescheduled",
    FollowUpCancelled: "Follow-up cancelled",
    FollowUpCompleted: "Follow-up completed",
  };

  return labels[type] || type;
}

function emptyLeadOptions() {
  return {
    branches: [],
    courses: [],
    sources: [],
    stages: [],
    counselors: [],
  };
}

function emptyLeadList() {
  return {
    items: [],
    page: 1,
    pageSize: 25,
    total: 0,
  };
}

function defaultLeadFilters() {
  return {
    search: "",
    branchId: "",
    courseId: "",
    sourceId: "",
    stageId: "",
    assignedUserId: "",
    priority: "",
    archive: "active",
    sort: "newest",
    page: 1,
    pageSize: 25,
  };
}

function createDefaultLeadForm(options) {
  return {
    studentName: "",
    guardianName: "",
    email: "",
    phone: "",
    city: "",
    courseId: options.courses[0]?.id || "",
    leadSourceId: options.sources[0]?.id || "",
    leadStageId: findOptionId(options.stages, "New Inquiry") || options.stages[0]?.id || "",
    branchId: options.branches[0]?.id || "",
    assignedUserId: options.counselors[0]?.id || "",
    status: "New Lead",
    priority: "Medium",
    nextFollowUpAt: "",
  };
}

function createLeadUpdateForm(lead) {
  return {
    studentName: lead?.studentName || "",
    guardianName: lead?.guardianName || "",
    email: lead?.email || "",
    phone: lead?.phone || "",
    city: lead?.city || "",
    courseId: lead?.courseId || "",
    leadSourceId: lead?.leadSourceId || "",
    leadStageId: lead?.leadStageId || "",
    branchId: lead?.branchId || "",
    assignedUserId: lead?.assignedUserId || "",
    status: lead?.status || "New Lead",
    priority: lead?.priority || "Medium",
    nextFollowUpAt: toDateTimeLocalValue(lead?.nextFollowUpAt),
  };
}

function includeCurrentOption(options, id, name) {
  if (!id || options.some((item) => item.id === id)) {
    return options;
  }

  return [{ id, name: name || "Inactive value", inactive: true }, ...options];
}

function createDefaultFollowUpForm(lead) {
  return {
    type: "Call",
    priority: lead?.priority || "Medium",
    assignedUserId: lead?.assignedUserId || "",
    dueAt: "",
  };
}

function validateLeadForm(form) {
  const errors = {};
  const phoneDigits = form.phone.replace(/\D/g, "");

  if (!form.studentName.trim()) {
    errors.studentName = "Student name is required.";
  }

  if (!form.email.trim()) {
    errors.email = "Email is required.";
  } else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.email.trim())) {
    errors.email = "Enter a valid email address.";
  }

  if (!form.phone.trim()) {
    errors.phone = "Phone is required.";
  } else if (phoneDigits.length < 10 || phoneDigits.length > 15) {
    errors.phone = "Enter 10 to 15 phone digits.";
  }

  if (!form.courseId) {
    errors.courseId = "Course is required.";
  }

  if (!form.leadSourceId) {
    errors.leadSourceId = "Source is required.";
  }

  if (!form.leadStageId) {
    errors.leadStageId = "Stage is required.";
  }

  if (form.nextFollowUpAt && new Date(form.nextFollowUpAt).getTime() < Date.now() - 5 * 60 * 1000) {
    errors.nextFollowUpAt = "Next follow-up cannot be in the past.";
  }

  return errors;
}

function validateUserForm(form, isEditing) {
  const errors = {};

  if (!form.fullName.trim()) {
    errors.fullName = "Full name is required.";
  }

  if (!isEditing) {
    if (!form.email.trim()) {
      errors.email = "Email is required.";
    } else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.email.trim())) {
      errors.email = "Enter a valid email address.";
    }

    const passwordError = getPasswordPolicyError(form.password);
    if (passwordError) {
      errors.password = passwordError;
    }
  }

  return errors;
}

function validatePasswordChangeForm(form) {
  const errors = validatePasswordResetForm(form);

  if (!form.currentPassword) {
    errors.currentPassword = "Current password is required.";
  }

  if (form.currentPassword && form.newPassword && form.currentPassword === form.newPassword) {
    errors.newPassword = "New password must be different from the current password.";
  }

  return errors;
}

function validatePasswordResetForm(form) {
  const errors = {};
  const passwordError = getPasswordPolicyError(form.newPassword);
  if (passwordError) {
    errors.newPassword = passwordError;
  }

  if (!form.confirmPassword) {
    errors.confirmPassword = "Password confirmation is required.";
  } else if (form.newPassword !== form.confirmPassword) {
    errors.confirmPassword = "Password confirmation does not match.";
  }

  return errors;
}

function getPasswordPolicyError(password) {
  if (!password) {
    return "Password is required.";
  }

  if (password.length < 8 || password.length > 120) {
    return "Password must be 8 to 120 characters.";
  }

  if (/\s/.test(password)) {
    return "Password cannot contain spaces.";
  }

  if (!/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/\d/.test(password) || !/[^A-Za-z0-9]/.test(password)) {
    return "Use uppercase, lowercase, number, and special character.";
  }

  return "";
}

function findOptionId(options, name) {
  return options.find((item) => item.name === name)?.id || "";
}

function findOptionIdByName(options, name) {
  if (!name || name === "Unassigned") {
    return "";
  }

  return options.find((item) => item.name === name)?.id || "";
}

function getManagedRoleOptions(currentUser) {
  const roles = ["Admin", "BranchManager", "Counselor", "Telecaller", "Accountant", "ReadOnly"];
  return currentUser?.role === "Owner" ? ["Owner", ...roles] : roles;
}

function canEditUser(currentUser, user, canManageUsers) {
  if (!canManageUsers || !currentUser || !user) {
    return false;
  }

  return user.role !== "Owner" || currentUser.role === "Owner";
}

function canResetUserPassword(currentUser, user, canManageUsers) {
  if (!canManageUsers || !currentUser || !user || user.id === currentUser.userId) {
    return false;
  }

  return user.role !== "Owner" || currentUser.role === "Owner";
}

function formatRole(role) {
  return role === "ReadOnly" ? "Read Only" : role.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function firstError(value) {
  return Array.isArray(value) ? value[0] : value;
}

function optionalValue(value) {
  return value && value.trim() ? value.trim() : null;
}

function toDateTimeLocalValue(value) {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const offsetDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return offsetDate.toISOString().slice(0, 16);
}

export default App;
