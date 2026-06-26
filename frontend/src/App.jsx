import {
  BarChart3,
  Bell,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  Download,
  FileText,
  GraduationCap,
  LayoutDashboard,
  LogOut,
  Menu,
  MoreVertical,
  Plus,
  Search,
  Settings,
  Users,
  X,
} from "lucide-react";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
  addLeadActivity,
  completeLeadFollowUp,
  createLead,
  createLeadFollowUp,
  getCrmData,
  getCurrentUser,
  getLeadDetail,
  getPlatformTenants,
  getStoredAuth,
  login,
  logout,
  createPlatformTenant,
  updateLead,
} from "./api";
import { activities, counselors, stages as fallbackStages } from "./data/mockData";

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
  const [crmData, setCrmData] = useState({
    dashboard: null,
    leads: [],
    pipeline: [],
    followUps: [],
    leadOptions: emptyLeadOptions(),
  });
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

  const handleLogout = () => {
    logout();
    setCurrentUser(null);
    setLeadModalOpen(false);
    setSelectedLeadId("");
    setLeadDetail(null);
    setCrmData({
      dashboard: null,
      leads: [],
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

  const handleCreateLead = async (payload) => {
    setCreateStatus({ saving: true, error: "", fieldErrors: {} });
    try {
      await createLead(payload);
      setLeadModalOpen(false);
      setActivePage("leads");
      await loadCrmData();
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
      setLeadActionStatus({ saving: false, error: "", fieldErrors: {} });
    } catch (error) {
      setLeadActionStatus({
        saving: false,
        error: error instanceof Error ? error.message : "Unable to update lead.",
        fieldErrors: error?.errors || {},
      });
    }
  };

  if (authStatus.loading) {
    return <StatePanel title="Checking session" message="Validating your CounselMate login..." />;
  }

  if (!currentUser) {
    return <LoginScreen error={authStatus.error} signingIn={authStatus.signingIn} onSubmit={handleLogin} />;
  }

  const canManageLeads = ["Owner", "Admin", "BranchManager", "Counselor", "Telecaller"].includes(currentUser.role);

  return (
    <div className="app-shell">
      <aside className={`sidebar ${sidebarOpen ? "is-open" : ""}`}>
        <div className="brand">
          <div className="brand-mark">
            <GraduationCap size={24} />
          </div>
          <div>
            <strong>CounselMate</strong>
            <span>Admission CRM</span>
          </div>
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
              loading={crmStatus.loading}
              error={crmStatus.error}
              onRetry={loadCrmData}
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
          {activePage === "counselors" && <CounselorsPage />}
          {activePage === "reports" && <ReportsPage />}
          {activePage === "settings" && <SettingsPage stages={crmData.pipeline} />}
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
          onAddActivity={(payload) => runLeadAction((leadId) => addLeadActivity(leadId, payload))}
          onCreateFollowUp={(payload) => runLeadAction((leadId) => createLeadFollowUp(leadId, payload))}
          onCompleteFollowUp={(followUpId) => runLeadAction((leadId) => completeLeadFollowUp(leadId, followUpId))}
          canManageLeads={canManageLeads}
        />
      )}
    </div>
  );
}

function LoginScreen({ error, signingIn, onSubmit }) {
  const [form, setForm] = useState({
    tenantSlug: "demo-academy",
    email: "rahul@demo-academy.test",
    password: "",
  });

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      tenantSlug: form.tenantSlug.trim(),
      email: form.email.trim(),
      password: form.password,
    });
  };

  return (
    <main className="login-shell">
      <section className="login-panel">
        <div className="brand login-brand">
          <div className="brand-mark">
            <GraduationCap size={24} />
          </div>
          <div>
            <strong>CounselMate</strong>
            <span>Admission CRM</span>
          </div>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <div>
            <h1>Sign in</h1>
            <p>Use your institute account to continue.</p>
          </div>

          {error && <div className="form-alert">{error}</div>}

          <Field label="Tenant" required>
            <input value={form.tenantSlug} maxLength={80} onChange={(event) => setForm((current) => ({ ...current, tenantSlug: event.target.value }))} required />
          </Field>

          <Field label="Email" required>
            <input value={form.email} type="email" maxLength={240} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} required />
          </Field>

          <Field label="Password" required>
            <input value={form.password} type="password" maxLength={120} onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))} required autoFocus />
          </Field>

          <button className="primary-button" type="submit" disabled={signingIn}>
            {signingIn ? "Signing in..." : "Sign in"}
          </button>

          <p className="login-hint">Demo: rahul@demo-academy.test / Demo@12345</p>
        </form>
      </section>
    </main>
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

function LeadsPage({ leads, loading, error, onRetry, onNewLead, onOpenLead, canManageLeads }) {
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
      <FilterBar />
      <LeadsTable leads={leads} loading={loading} error={error} onRetry={onRetry} onOpenLead={onOpenLead} />
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
  onAddActivity,
  onCreateFollowUp,
  onCompleteFollowUp,
  canManageLeads,
}) {
  const [editForm, setEditForm] = useState(() => createLeadUpdateForm(lead));
  const [noteForm, setNoteForm] = useState({ type: "Note", description: "" });
  const [followUpForm, setFollowUpForm] = useState(() => createDefaultFollowUpForm(lead));

  useEffect(() => {
    setEditForm(createLeadUpdateForm(lead));
    setFollowUpForm(createDefaultFollowUpForm(lead));
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
  const canSave = Boolean(lead && editForm.leadStageId);

  const handleUpdateSubmit = async (event) => {
    event.preventDefault();
    if (!canSave) {
      return;
    }

    await onUpdate({
      leadStageId: editForm.leadStageId,
      assignedUserId: optionalValue(editForm.assignedUserId),
      status: editForm.status,
      priority: editForm.priority,
      nextFollowUpAt: editForm.nextFollowUpAt ? new Date(editForm.nextFollowUpAt).toISOString() : null,
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
              <Status status={lead.status} />
            </section>

            <div className="detail-grid">
              <InfoItem label="Phone" value={lead.phone} />
              <InfoItem label="Email" value={lead.email} />
              <InfoItem label="Guardian" value={lead.guardianName || "Not added"} />
              <InfoItem label="City" value={lead.city || "Not added"} />
              <InfoItem label="Branch" value={lead.branch || "No branch"} />
              <InfoItem label="Created" value={formatDate(lead.createdAt)} />
            </div>

            <form className="drawer-section" onSubmit={handleUpdateSubmit}>
              <div className="section-heading">
                <h3>Lead Status</h3>
                <button type="submit" className="primary-button" disabled={!canManageLeads || !canSave || actionStatus.saving}>
                  {actionStatus.saving ? "Saving..." : "Save Changes"}
                </button>
              </div>
              <div className="form-grid compact">
                <Field label="Stage" error={getFieldError("leadStageId")} required>
                  <select value={editForm.leadStageId} onChange={(event) => setEditForm((current) => ({ ...current, leadStageId: event.target.value }))} required>
                    {options.stages.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                  </select>
                </Field>
                <Field label="Status" error={getFieldError("status")}>
                  <select value={editForm.status} onChange={(event) => setEditForm((current) => ({ ...current, status: event.target.value }))}>
                    {["New Lead", "Interested", "Follow Up", "Enrolled", "Dropped"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Priority" error={getFieldError("priority")}>
                  <select value={editForm.priority} onChange={(event) => setEditForm((current) => ({ ...current, priority: event.target.value }))}>
                    {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                  <select value={editForm.assignedUserId} onChange={(event) => setEditForm((current) => ({ ...current, assignedUserId: event.target.value }))}>
                    <option value="">Unassigned</option>
                    {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                  </select>
                </Field>
                <Field label="Next Follow-up" error={getFieldError("nextFollowUpAt")} className="span-2">
                  <input type="datetime-local" value={editForm.nextFollowUpAt} onChange={(event) => setEditForm((current) => ({ ...current, nextFollowUpAt: event.target.value }))} />
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
                <button type="submit" className="primary-button" disabled={!canManageLeads || !followUpForm.dueAt || actionStatus.saving}>
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
              <div className="detail-list">
                {lead.followUps.length === 0 && <p className="muted-text">No follow-ups scheduled.</p>}
                {lead.followUps.map((item) => (
                  <div className="detail-list-item" key={item.id}>
                    <div>
                      <strong>{item.type} · {formatDate(item.dueAt)}</strong>
                      <p>{formatTime(item.dueAt)} · {item.assignedTo}</p>
                    </div>
                    <div className="detail-list-actions">
                      <Badge label={item.status} muted={item.status !== "Completed"} />
                      {canManageLeads && item.status !== "Completed" && (
                        <button type="button" className="ghost-button" onClick={() => onCompleteFollowUp(item.id)} disabled={actionStatus.saving}>
                          <CheckCircle2 size={16} />
                          Complete
                        </button>
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
                {lead.activities.map((activity) => (
                  <div className="timeline-item" key={activity.id}>
                    <span />
                    <div>
                      <strong>{activity.type}</strong>
                      <p>{activity.description}</p>
                      <small>{formatDate(activity.createdAt)}, {formatTime(activity.createdAt)} · {activity.createdBy}</small>
                    </div>
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

function CounselorsPage() {
  return (
    <>
      <PageTitle title="Counsellors" subtitle="Monitor team workload, ownership, and conversion performance." />
      <Card title="Team Performance">
        <div className="team-list">
          {counselors.map((person) => (
            <div className="team-row" key={person.name}>
              <div className="avatar">{initials(person.name)}</div>
              <div>
                <strong>{person.name}</strong>
                <p>{person.role}</p>
              </div>
              <span>{person.leads} Leads</span>
              <span>{person.conversion}</span>
              <button className="ghost-button">View</button>
            </div>
          ))}
        </div>
      </Card>
    </>
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

function SettingsPage({ stages }) {
  const visibleStages = stages.length ? stages : fallbackStages;

  return (
    <>
      <PageTitle
        title="System Settings"
        subtitle="Manage institution details, team access, workflow, and CRM automation."
        action={
          <div className="button-row">
            <button className="ghost-button">Discard Changes</button>
            <button className="primary-button">Save Preferences</button>
          </div>
        }
      />
      <div className="settings-layout">
        <Card title="Settings Menu">
          {["Institute Profile", "Team Management", "Workflow Configuration", "Alerts & Notifications", "Integrations & API"].map((item, index) => (
            <button className={`settings-menu ${index === 0 ? "active" : ""}`} key={item}>{item}</button>
          ))}
        </Card>
        <div className="settings-content">
          <Card title="Institute Profile">
            <div className="form-grid">
              <label>
                Institute Legal Name
                <input defaultValue="Global Heights Academy" />
              </label>
              <label>
                Primary Contact Email
                <input defaultValue="admissions@gha.edu" />
              </label>
              <label className="span-2">
                Registered Office Address
                <textarea defaultValue="124 Education Plaza, Academic District, New Delhi" />
              </label>
            </div>
          </Card>
          <Card title="Lead Stages">
            <div className="stage-list">
              {visibleStages.map((stage) => (
                <div key={stage.name}>
                  <MoreVertical size={18} />
                  {stage.name}
                  <span />
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </>
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

function FilterBar() {
  return (
    <div className="filter-bar">
      {["Lead Status", "Course Interest", "Date Range"].map((label) => (
        <label key={label}>
          {label}
          <button>
            {label === "Date Range" ? "Oct 1 - Oct 31, 2026" : label === "Lead Status" ? "All Statuses" : "All Courses"}
            <ChevronDown size={18} />
          </button>
        </label>
      ))}
      <button className="soft-button">Reset</button>
      <button className="primary-button">Apply</button>
    </div>
  );
}

function LeadsTable({ leads, loading, error, onRetry, onOpenLead }) {
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
              <td><Status status={lead.status} /></td>
              <td><MoreVertical size={20} /></td>
            </tr>
          ))}
        </tbody>
      </table>
      )}
      <footer className="table-footer">
        <span>Showing {loading || error ? 0 : leads.length} of {loading || error ? 0 : leads.length} leads</span>
        <div>
          <button className="pager active">1</button>
          <button className="pager">2</button>
          <button className="pager">3</button>
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

function emptyLeadOptions() {
  return {
    branches: [],
    courses: [],
    sources: [],
    stages: [],
    counselors: [],
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
    leadStageId: lead?.leadStageId || "",
    assignedUserId: lead?.assignedUserId || "",
    status: lead?.status || "New Lead",
    priority: lead?.priority || "Medium",
    nextFollowUpAt: toDateTimeLocalValue(lead?.nextFollowUpAt),
  };
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

function findOptionId(options, name) {
  return options.find((item) => item.name === name)?.id || "";
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
