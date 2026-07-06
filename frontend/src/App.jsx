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
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  addLeadActivity,
  addLeadPaymentTransaction,
  applyCommunicationTemplate,
  archiveLead,
  cancelLeadFollowUp,
  cancelLeadPayment,
  changePassword,
  createCommunicationTemplate,
  commitLeadImport,
  completeLeadFollowUp,
  createLead,
  createLeadApplication,
  createLeadFollowUp,
  createLeadPayment,
  deleteLeadDocument,
  downloadLeadDocument,
  downloadLeadImportTemplate,
  exportLeads,
  exportReports,
  forgotPassword,
  getCrmData,
  getCurrentUser,
  getLeadDetail,
  getLeads,
  getReports,
  getCounsellorWorkInsights,
  getApplicationDetail,
  getApplications,
  getEnrollmentDetail,
  getEnrollments,
  getCommunicationTemplates,
  getCurrentTenant,
  getPlatformTenants,
  getStoredAuth,
  getUsers,
  getNotifications,
  getNotificationUnreadCount,
  login,
  logout,
  markAllNotificationsRead,
  markNotificationRead,
  previewLeadImport,
  createUser,
  createPlatformTenant,
  createMasterRecord,
  getMasterData,
  reorderLeadStages,
  resetUserPassword,
  rescheduleLeadFollowUp,
  rejectLeadDocument,
  updateMasterRecord,
  updateCurrentTenant,
  updateCommunicationTemplate,
  updateStoredUser,
  updateUser,
  updateLead,
  updateLeadPayment,
  uploadLeadDocument,
  verifyLeadDocument,
  restoreLead,
  runBulkLeadAction,
  transitionApplication,
  updateApplicationChecklistItem,
  enrollApplication,
  updateEnrollmentStatus,
} from "./api";
import counselMateLogo from "./assets/counselmate-logo.png";

const navItems = [
  { id: "platform", label: "Platform", icon: Users, ownerOnly: true },
  { id: "dashboard", label: "Dashboard", icon: LayoutDashboard },
  { id: "leads", label: "Leads", icon: Search },
  { id: "pipeline", label: "Pipeline", icon: BarChart3 },
  { id: "followups", label: "Follow-ups", icon: CalendarDays },
  { id: "applications", label: "Applications", icon: BookOpen },
  { id: "enrollments", label: "Enrollments", icon: UserCheck },
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
    communicationTemplates: [],
  });
  const [leadFilters, setLeadFilters] = useState(() => defaultLeadFilters());
  const [crmStatus, setCrmStatus] = useState({
    loading: true,
    error: "",
  });
  const [leadModalOpen, setLeadModalOpen] = useState(false);
  const [leadImportOpen, setLeadImportOpen] = useState(false);
  const [leadModalDefaults, setLeadModalDefaults] = useState({});
  const [createStatus, setCreateStatus] = useState({
    saving: false,
    error: "",
    fieldErrors: {},
  });
  const [leadExportStatus, setLeadExportStatus] = useState({
    exporting: "",
    error: "",
  });
  const [reportsData, setReportsData] = useState(null);
  const [reportFilters, setReportFilters] = useState(() => defaultReportFilters());
  const [reportsStatus, setReportsStatus] = useState({
    loading: false,
    error: "",
    exporting: "",
  });
  const [applicationFilters, setApplicationFilters] = useState({ status: "", search: "", page: 1, pageSize: 25 });
  const [applicationsData, setApplicationsData] = useState({ items: [], page: 1, pageSize: 25, total: 0 });
  const [applicationStatus, setApplicationStatus] = useState({ loading: false, saving: false, error: "", message: "" });
  const [selectedApplication, setSelectedApplication] = useState(null);
  const [enrollmentFilters, setEnrollmentFilters] = useState({ status: "", search: "", courseId: "", branchId: "", intake: "", page: 1, pageSize: 25 });
  const [enrollmentsData, setEnrollmentsData] = useState({ items: [], page: 1, pageSize: 25, total: 0 });
  const [enrollmentStatus, setEnrollmentStatus] = useState({ loading: false, saving: false, error: "", message: "" });
  const [selectedEnrollment, setSelectedEnrollment] = useState(null);
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
  const [followUpQueueStatus, setFollowUpQueueStatus] = useState({
    savingId: "",
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
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [notificationData, setNotificationData] = useState({ items: [], total: 0, unreadCount: 0, page: 1 });
  const [notificationStatus, setNotificationStatus] = useState({ loading: false, error: "", saving: false });
  const activeLabel = navItems.find((item) => item.id === activePage)?.label || "Dashboard";

  const loadNotifications = useCallback(async ({ page = 1, append = false, silent = false } = {}) => {
    if (!currentUser) {
      return;
    }

    if (!silent) {
      setNotificationStatus((current) => ({ ...current, loading: true, error: "" }));
    }
    try {
      const response = await getNotifications({ page, pageSize: 15 });
      setNotificationData((current) => ({
        ...response,
        items: append ? [...current.items, ...response.items] : response.items,
      }));
      setNotificationStatus((current) => ({ ...current, loading: false, error: "" }));
    } catch (error) {
      if (!silent) {
        setNotificationStatus((current) => ({
          ...current,
          loading: false,
          error: error instanceof Error ? error.message : "Unable to load notifications.",
        }));
      }
    }
  }, [currentUser]);

  const refreshUnreadCount = useCallback(async () => {
    if (!currentUser) {
      return;
    }
    try {
      const response = await getNotificationUnreadCount();
      setNotificationData((current) => ({ ...current, unreadCount: response.count }));
    } catch {
      // Polling failures stay silent; opening the inbox exposes actionable errors.
    }
  }, [currentUser]);

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

  const loadReports = useCallback(async (filters = reportFilters) => {
    if (!currentUser) {
      return;
    }

    setReportsStatus((current) => ({ ...current, loading: true, error: "" }));
    try {
      const reports = ["Counselor", "Telecaller"].includes(currentUser.role)
        ? await getCounsellorWorkInsights(filters)
        : await getReports(filters);
      setReportsData(reports);
      setReportsStatus((current) => ({ ...current, loading: false, error: "" }));
    } catch (error) {
      setReportsStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load reports.",
      }));
    }
  }, [currentUser, reportFilters]);

  const loadApplications = useCallback(async (filters = applicationFilters) => {
    if (!currentUser) return;
    setApplicationStatus((current) => ({ ...current, loading: true, error: "", message: "" }));
    try {
      const response = await getApplications(filters);
      setApplicationsData(response);
      setApplicationStatus((current) => ({ ...current, loading: false, error: "" }));
    } catch (error) {
      setApplicationStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load applications.",
      }));
    }
  }, [currentUser, applicationFilters]);

  const loadEnrollments = useCallback(async (filters = enrollmentFilters) => {
    if (!currentUser) return;
    setEnrollmentStatus((current) => ({ ...current, loading: true, error: "", message: "" }));
    try {
      const response = await getEnrollments(filters);
      setEnrollmentsData(response);
      setEnrollmentStatus((current) => ({ ...current, loading: false, error: "" }));
    } catch (error) {
      setEnrollmentStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load enrollments.",
      }));
    }
  }, [currentUser, enrollmentFilters]);

  useEffect(() => {
    const restoreSession = async () => {
      const { token } = getStoredAuth();
      if (!token) {
        setAuthStatus({ loading: false, error: "", signingIn: false });
        return;
      }

      try {
        const user = await getCurrentUser();
        updateStoredUser(user);
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

  useEffect(() => {
    if (currentUser && activePage === "reports") {
      loadReports();
    }
  }, [activePage, currentUser, loadReports]);

  useEffect(() => {
    if (currentUser && activePage === "applications") {
      loadApplications();
    }
  }, [activePage, currentUser, loadApplications]);

  useEffect(() => {
    if (currentUser && activePage === "enrollments") {
      loadEnrollments();
    }
  }, [activePage, currentUser, loadEnrollments]);

  useEffect(() => {
    if (!currentUser) {
      setNotificationOpen(false);
      setNotificationData({ items: [], total: 0, unreadCount: 0, page: 1 });
      return undefined;
    }

    refreshUnreadCount();
    const timer = window.setInterval(refreshUnreadCount, 60000);
    return () => window.clearInterval(timer);
  }, [currentUser, refreshUnreadCount]);

  const toggleNotifications = () => {
    setNotificationOpen((open) => {
      if (!open) {
        loadNotifications();
      }
      return !open;
    });
  };

  const handleNotificationClick = async (notification) => {
    if (!notification.readAt) {
      try {
        await markNotificationRead(notification.id);
        setNotificationData((current) => ({
          ...current,
          unreadCount: Math.max(0, current.unreadCount - 1),
          items: current.items.map((item) => item.id === notification.id
            ? { ...item, readAt: new Date().toISOString() }
            : item),
        }));
      } catch (error) {
        setNotificationStatus((current) => ({
          ...current,
          error: error instanceof Error ? error.message : "Unable to update notification.",
        }));
        return;
      }
    }

    setNotificationOpen(false);
    if (notification.leadId) {
      await openLeadDetail(notification.leadId);
    }
  };

  const handleMarkAllNotificationsRead = async () => {
    setNotificationStatus((current) => ({ ...current, saving: true, error: "" }));
    try {
      await markAllNotificationsRead();
      const readAt = new Date().toISOString();
      setNotificationData((current) => ({
        ...current,
        unreadCount: 0,
        items: current.items.map((item) => ({ ...item, readAt: item.readAt || readAt })),
      }));
      setNotificationStatus((current) => ({ ...current, saving: false }));
    } catch (error) {
      setNotificationStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to mark notifications as read.",
      }));
    }
  };

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
    setLeadImportOpen(false);
    setSelectedLeadId("");
    setLeadDetail(null);
    setLeadFilters(defaultLeadFilters());
    setLeadExportStatus({ exporting: "", error: "" });
    setReportsData(null);
    setReportFilters(defaultReportFilters());
    setReportsStatus({ loading: false, error: "", exporting: "" });
    setCrmData({
      dashboard: null,
      leads: emptyLeadList(),
      pipeline: [],
      followUps: [],
      leadOptions: emptyLeadOptions(),
      communicationTemplates: [],
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

  const handleTenantProfileChanged = (profile) => {
    setCurrentUser((user) => {
      if (!user) {
        return user;
      }
      const updatedUser = {
        ...user,
        tenantName: profile.name,
        tenantLogoUrl: profile.logoUrl,
        tenantBrandColor: profile.brandColor,
      };
      updateStoredUser(updatedUser);
      return updatedUser;
    });
  };

  const openCreateLeadModal = (stageName = "") => {
    const defaults = {};
    if (stageName) {
      defaults.leadStageId = findOptionId(crmData.leadOptions.stages, stageName) || "";
    }
    if (["Counselor", "Telecaller"].includes(currentUser.role)) {
      defaults.branchId = "";
      defaults.assignedUserId = currentUser.userId;
    }
    setLeadModalDefaults(defaults);
    setCreateStatus({ saving: false, error: "", fieldErrors: {} });
    setLeadModalOpen(true);
  };

  const handleCreateLead = async (payload) => {
    setCreateStatus({ saving: true, error: "", fieldErrors: {} });
    try {
      await createLead(payload);
      setLeadModalOpen(false);
      setLeadModalDefaults({});
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

  const handleExportLeads = async (format) => {
    setLeadExportStatus({ exporting: format, error: "" });
    try {
      const response = await exportLeads(leadFilters, format);
      downloadFileResponse(response, `leads.${format}`);
      setLeadExportStatus({ exporting: "", error: "" });
    } catch (error) {
      setLeadExportStatus({
        exporting: "",
        error: error instanceof Error ? error.message : "Unable to export leads.",
      });
    }
  };

  const handleLeadImportFinished = async () => {
    setActivePage("leads");
    await loadCrmData();
    await loadLeadList();
  };

  const handleApplicationFiltersChange = async (updates) => {
    const next = { ...applicationFilters, ...updates, page: updates.page || 1 };
    setApplicationFilters(next);
    await loadApplications(next);
  };

  const handleEnrollmentFiltersChange = async (updates) => {
    const next = { ...enrollmentFilters, ...updates, page: updates.page || 1 };
    setEnrollmentFilters(next);
    await loadEnrollments(next);
  };

  const openEnrollmentDetail = async (enrollmentId) => {
    setEnrollmentStatus((current) => ({ ...current, saving: true, error: "", message: "" }));
    try {
      const detail = await getEnrollmentDetail(enrollmentId);
      setSelectedEnrollment(detail);
      setEnrollmentStatus((current) => ({ ...current, saving: false, error: "" }));
    } catch (error) {
      setEnrollmentStatus((current) => ({ ...current, saving: false, error: error instanceof Error ? error.message : "Unable to load enrollment." }));
    }
  };

  const handleEnrollmentStatusUpdate = async (enrollment, status, note) => {
    setEnrollmentStatus((current) => ({ ...current, saving: true, error: "", message: "" }));
    try {
      const detail = await updateEnrollmentStatus(enrollment.id, { status, note: optionalValue(note), version: enrollment.version });
      setSelectedEnrollment(detail);
      await loadEnrollments();
      setEnrollmentStatus((current) => ({ ...current, saving: false, error: "", message: "Enrollment status updated." }));
      return detail;
    } catch (error) {
      setEnrollmentStatus((current) => ({ ...current, saving: false, error: error instanceof Error ? error.message : "Unable to update enrollment.", message: "" }));
      return null;
    }
  };

  const openApplicationDetail = async (applicationId) => {
    setApplicationStatus((current) => ({ ...current, saving: true, error: "", message: "" }));
    try {
      const detail = await getApplicationDetail(applicationId);
      setSelectedApplication(detail);
      setApplicationStatus((current) => ({ ...current, saving: false, error: "" }));
    } catch (error) {
      setApplicationStatus((current) => ({ ...current, saving: false, error: error instanceof Error ? error.message : "Unable to load application." }));
    }
  };

  const runApplicationAction = async (action, successMessage = "Application updated.") => {
    setApplicationStatus((current) => ({ ...current, saving: true, error: "", message: "" }));
    try {
      const detail = await action();
      setSelectedApplication(detail);
      await loadApplications();
      await loadCrmData();
      setApplicationStatus((current) => ({ ...current, saving: false, error: "", message: successMessage }));
      return detail;
    } catch (error) {
      setApplicationStatus((current) => ({ ...current, saving: false, error: error instanceof Error ? error.message : "Unable to update application.", message: "" }));
      return null;
    }
  };

  const runApplicationDocumentAction = (application, action, successMessage = "Documents updated.") => runApplicationAction(async () => {
    await action(application.leadId);
    return getApplicationDetail(application.id);
  }, successMessage);

  const runApplicationPaymentAction = (application, action, successMessage = "Fee ledger updated.") => runApplicationAction(async () => {
    await action(application.leadId);
    return getApplicationDetail(application.id);
  }, successMessage);

  const handleApplicationDocumentDownload = async (application, document) => {
    if (!application?.leadId || !document?.documentId) {
      return;
    }

    setApplicationStatus((current) => ({ ...current, saving: false, error: "", message: "" }));
    try {
      const response = await downloadLeadDocument(application.leadId, document.documentId);
      downloadFileResponse(response, document.fileName || `${document.name}.pdf`);
    } catch (error) {
      setApplicationStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to download document.",
        message: "",
      }));
    }
  };

  const handleCreateApplicationFromLead = async () => {
    if (!selectedLeadId) return null;
    const detail = await runApplicationAction(
      () => createLeadApplication(selectedLeadId, {}),
      "Application created.",
    );
    if (detail) {
      setActivePage("applications");
    }
    return detail;
  };

  const handleBulkLeadAction = async (payload) => {
    const response = await runBulkLeadAction(payload);
    await loadCrmData();
    await loadLeadList();
    return response;
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
      return detail;
    } catch (error) {
      setLeadActionStatus({
        saving: false,
        error: error instanceof Error ? error.message : "Unable to update lead.",
        fieldErrors: error?.errors || {},
      });
      return null;
    }
  };

  const runLeadDocumentAction = (action) => runLeadAction(async (leadId) => {
    await action(leadId);
    return getLeadDetail(leadId);
  });

  const handleLeadDocumentDownload = async (document) => {
    if (!selectedLeadId || !document?.documentId) {
      return;
    }

    setLeadActionStatus({ saving: false, error: "", fieldErrors: {} });
    try {
      const response = await downloadLeadDocument(selectedLeadId, document.documentId);
      downloadFileResponse(response, document.fileName || `${document.name}.pdf`);
    } catch (error) {
      setLeadActionStatus({
        saving: false,
        error: error instanceof Error ? error.message : "Unable to download document.",
        fieldErrors: error?.errors || {},
      });
    }
  };

  const runFollowUpQueueAction = async (followUp, action) => {
    setFollowUpQueueStatus({ savingId: followUp.id, error: "", fieldErrors: {} });
    try {
      const detail = await action();
      await loadCrmData();
      await loadLeadList();
      if (selectedLeadId && detail?.id === selectedLeadId) {
        setLeadDetail(detail);
      }
      setFollowUpQueueStatus({ savingId: "", error: "", fieldErrors: {} });
      return detail;
    } catch (error) {
      setFollowUpQueueStatus({
        savingId: "",
        error: error instanceof Error ? error.message : "Unable to update follow-up.",
        fieldErrors: error?.errors || {},
      });
      return null;
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

  const handleReportFiltersChange = async (updates) => {
    const nextFilters = { ...reportFilters, ...updates };
    setReportFilters(nextFilters);
    await loadReports(nextFilters);
  };

  const handleReportFiltersReset = async () => {
    const nextFilters = defaultReportFilters();
    setReportFilters(nextFilters);
    await loadReports(nextFilters);
  };

  const handleExportReports = async (format) => {
    setReportsStatus((current) => ({ ...current, exporting: format, error: "" }));
    try {
      const response = await exportReports(reportFilters, format);
      downloadFileResponse(response, `reports.${format}`);
      setReportsStatus((current) => ({ ...current, exporting: "", error: "" }));
    } catch (error) {
      setReportsStatus((current) => ({
        ...current,
        exporting: "",
        error: error instanceof Error ? error.message : "Unable to export reports.",
      }));
    }
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
  const canArchiveLeads = ["Owner", "Admin", "BranchManager"].includes(currentUser.role);
  const canManageUsers = ["Owner", "Admin"].includes(currentUser.role);
  const canManagePayments = ["Owner", "Admin", "Accountant"].includes(currentUser.role);

  return (
    <div className="app-shell" style={{ "--tenant-brand": currentUser.tenantBrandColor || "#2171D3" }}>
      <aside className={`sidebar ${sidebarOpen ? "is-open" : ""}`}>
        <div className="brand">
          <div className="brand-copy">
            <strong>CounselMate</strong>
            <span>Admission CRM</span>
          </div>
        </div>
        <TenantIdentity user={currentUser} />

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

        <button className="sidebar-action" onClick={() => openCreateLeadModal()} disabled={!canManageLeads}>
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
            <button
              className="icon-button"
              onClick={toggleNotifications}
              aria-label="Notifications"
              aria-expanded={notificationOpen}
            >
              <Bell size={20} />
              {notificationData.unreadCount > 0 && (
                <span className="notification-badge">
                  {notificationData.unreadCount > 99 ? "99+" : notificationData.unreadCount}
                </span>
              )}
            </button>
            {notificationOpen && (
              <NotificationPanel
                data={notificationData}
                status={notificationStatus}
                onClose={() => setNotificationOpen(false)}
                onItemClick={handleNotificationClick}
                onMarkAllRead={handleMarkAllNotificationsRead}
                onRetry={() => loadNotifications()}
                onLoadMore={() => loadNotifications({ page: notificationData.page + 1, append: true })}
              />
            )}
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
              onNewLead={() => openCreateLeadModal()}
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
              onNewLead={() => openCreateLeadModal()}
              onImport={() => setLeadImportOpen(true)}
              onExport={handleExportLeads}
              onOpenLead={openLeadDetail}
              onBulkAction={handleBulkLeadAction}
              canManageLeads={canManageLeads}
              canArchiveLeads={canArchiveLeads}
              currentUser={currentUser}
              canImportExportLeads={canManageUsers}
              exportStatus={leadExportStatus}
            />
          )}
          {activePage === "pipeline" && (
            <PipelinePage
              pipeline={crmData.pipeline}
              loading={crmStatus.loading}
              error={crmStatus.error}
              onRetry={loadCrmData}
              onNewLead={openCreateLeadModal}
              onOpenLead={openLeadDetail}
              canManageLeads={canManageLeads}
            />
          )}
          {activePage === "followups" && (
            <FollowUpsPage
              followUps={crmData.followUps}
              loading={crmStatus.loading}
              error={crmStatus.error}
              actionStatus={followUpQueueStatus}
              onRetry={loadCrmData}
              onOpenLead={openLeadDetail}
              onComplete={(followUp) => runFollowUpQueueAction(
                followUp,
                () => completeLeadFollowUp(followUp.leadId, followUp.id, { version: followUp.version }),
              )}
              onCancel={(followUp) => runFollowUpQueueAction(
                followUp,
                () => cancelLeadFollowUp(followUp.leadId, followUp.id, { version: followUp.version }),
              )}
              onReschedule={(followUp, payload) => runFollowUpQueueAction(
                followUp,
                () => rescheduleLeadFollowUp(followUp.leadId, followUp.id, payload),
              )}
              canManageLeads={canManageLeads}
            />
          )}
          {activePage === "applications" && (
            <ApplicationsPage
              applications={applicationsData}
              filters={applicationFilters}
              status={applicationStatus}
              selectedApplication={selectedApplication}
              onFiltersChange={handleApplicationFiltersChange}
              onRetry={() => loadApplications()}
              onOpen={openApplicationDetail}
              onCloseDetail={() => setSelectedApplication(null)}
              onTransition={(application, nextStatus, note) => runApplicationAction(
                () => transitionApplication(application.id, { status: nextStatus, note, version: application.version }),
                "Application status updated.",
              )}
              onChecklist={(application, item, updates) => runApplicationAction(
                () => updateApplicationChecklistItem(application.id, item.id, { ...updates, version: item.version }),
                "Checklist updated.",
              )}
              onEnroll={(application) => runApplicationAction(
                () => enrollApplication(application.id, { version: application.version }),
                "Enrollment completed.",
              )}
              onUploadDocument={(application, payload) => runApplicationDocumentAction(
                application,
                (leadId) => uploadLeadDocument(leadId, payload),
                "Document uploaded.",
              )}
              onVerifyDocument={(application, document) => runApplicationDocumentAction(
                application,
                (leadId) => verifyLeadDocument(leadId, document.documentId, { version: document.version }),
                "Document verified.",
              )}
              onRejectDocument={(application, document, notes) => runApplicationDocumentAction(
                application,
                (leadId) => rejectLeadDocument(leadId, document.documentId, { version: document.version, notes }),
                "Document rejected.",
              )}
              onDeleteDocument={(application, document) => runApplicationDocumentAction(
                application,
                (leadId) => deleteLeadDocument(leadId, document.documentId, { version: document.version }),
                "Document deleted.",
              )}
              onDownloadDocument={handleApplicationDocumentDownload}
              onCreatePayment={(application, payload) => runApplicationPaymentAction(
                application,
                (leadId) => createLeadPayment(leadId, payload),
                "Fee item added.",
              )}
              onUpdatePayment={(application, payment, payload) => runApplicationPaymentAction(
                application,
                (leadId) => updateLeadPayment(leadId, payment.id, payload),
                "Fee item updated.",
              )}
              onAddPaymentTransaction={(application, payment, payload) => runApplicationPaymentAction(
                application,
                (leadId) => addLeadPaymentTransaction(leadId, payment.id, payload),
                "Payment recorded.",
              )}
              onCancelPayment={(application, payment) => runApplicationPaymentAction(
                application,
                (leadId) => cancelLeadPayment(leadId, payment.id, { version: payment.version }),
                "Fee item cancelled.",
              )}
              currentUser={currentUser}
              canManageLeads={canManageLeads}
              canManagePayments={canManagePayments}
            />
          )}
          {activePage === "enrollments" && (
            <EnrollmentsPage
              enrollments={enrollmentsData}
              filters={enrollmentFilters}
              options={crmData.leadOptions}
              status={enrollmentStatus}
              selectedEnrollment={selectedEnrollment}
              currentUser={currentUser}
              onFiltersChange={handleEnrollmentFiltersChange}
              onRetry={() => loadEnrollments()}
              onOpen={openEnrollmentDetail}
              onCloseDetail={() => setSelectedEnrollment(null)}
              onStatusUpdate={handleEnrollmentStatusUpdate}
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
          {activePage === "reports" && (
            <ReportsPage
              reports={reportsData}
              options={crmData.leadOptions}
              filters={reportFilters}
              status={reportsStatus}
              canExportReports={canManageUsers}
              onFiltersChange={handleReportFiltersChange}
              onResetFilters={handleReportFiltersReset}
              onExport={handleExportReports}
              onRetry={() => loadReports()}
              currentUser={currentUser}
              onOpenLead={openLeadDetail}
            />
          )}
          {activePage === "settings" && (
            <SettingsPage
              currentUser={currentUser}
              onMasterDataChanged={loadCrmData}
              onTenantProfileChanged={handleTenantProfileChanged}
            />
          )}
        </section>
      </main>

      {leadModalOpen && canManageLeads && (
        <AddLeadModal
          options={crmData.leadOptions}
          initialValues={leadModalDefaults}
          saving={createStatus.saving}
          error={createStatus.error}
          fieldErrors={createStatus.fieldErrors}
          onClose={() => {
            if (!createStatus.saving) {
              setLeadModalOpen(false);
              setLeadModalDefaults({});
              setCreateStatus({ saving: false, error: "", fieldErrors: {} });
            }
          }}
          onSubmit={handleCreateLead}
        />
      )}

      {leadImportOpen && canManageUsers && (
        <LeadImportModal
          onClose={() => setLeadImportOpen(false)}
          onImported={handleLeadImportFinished}
        />
      )}

      {selectedLeadId && (
        <LeadDetailDrawer
          leadId={selectedLeadId}
          lead={leadDetail}
          options={crmData.leadOptions}
          communicationTemplates={crmData.communicationTemplates}
          loading={leadDetailStatus.loading}
          error={leadDetailStatus.error}
          actionStatus={leadActionStatus}
          currentUser={currentUser}
          onClose={closeLeadDetail}
          onRetry={() => openLeadDetail(selectedLeadId)}
          onUpdate={(payload) => runLeadAction((leadId) => updateLead(leadId, payload))}
          onArchive={(payload) => runLeadAction((leadId) => archiveLead(leadId, payload))}
          onRestore={(payload) => runLeadAction((leadId) => restoreLead(leadId, payload))}
          onAddActivity={(payload) => runLeadAction((leadId) => addLeadActivity(leadId, payload))}
          onApplyTemplate={(payload) => runLeadAction((leadId) => applyCommunicationTemplate(leadId, payload))}
          onCreateFollowUp={(payload) => runLeadAction((leadId) => createLeadFollowUp(leadId, payload))}
          onRescheduleFollowUp={(followUpId, payload) => runLeadAction((leadId) => rescheduleLeadFollowUp(leadId, followUpId, payload))}
          onCancelFollowUp={(followUpId, payload) => runLeadAction((leadId) => cancelLeadFollowUp(leadId, followUpId, payload))}
          onCompleteFollowUp={(followUpId, payload) => runLeadAction((leadId) => completeLeadFollowUp(leadId, followUpId, payload))}
          onUploadDocument={(payload) => runLeadDocumentAction((leadId) => uploadLeadDocument(leadId, payload))}
          onVerifyDocument={(document) => runLeadDocumentAction((leadId) => verifyLeadDocument(leadId, document.documentId, { version: document.version }))}
          onRejectDocument={(document, notes) => runLeadDocumentAction((leadId) => rejectLeadDocument(leadId, document.documentId, { version: document.version, notes }))}
          onDeleteDocument={(document) => runLeadDocumentAction((leadId) => deleteLeadDocument(leadId, document.documentId, { version: document.version }))}
          onDownloadDocument={handleLeadDocumentDownload}
          onCreatePayment={(payload) => runLeadDocumentAction((leadId) => createLeadPayment(leadId, payload))}
          onUpdatePayment={(payment, payload) => runLeadDocumentAction((leadId) => updateLeadPayment(leadId, payment.id, payload))}
          onAddPaymentTransaction={(payment, payload) => runLeadDocumentAction((leadId) => addLeadPaymentTransaction(leadId, payment.id, payload))}
          onCancelPayment={(payment) => runLeadDocumentAction((leadId) => cancelLeadPayment(leadId, payment.id, { version: payment.version }))}
          onCreateApplication={handleCreateApplicationFromLead}
          canManageLeads={canManageLeads}
          canArchiveLeads={canArchiveLeads}
          canManagePayments={canManagePayments}
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

function TenantIdentity({ user }) {
  const [imageFailed, setImageFailed] = useState(false);

  useEffect(() => {
    setImageFailed(false);
  }, [user.tenantLogoUrl]);

  return (
    <div className="tenant-identity">
      <div className="tenant-identity-mark" aria-hidden="true">
        {user.tenantLogoUrl && !imageFailed
          ? <img src={user.tenantLogoUrl} alt="" onError={() => setImageFailed(true)} />
          : <span>{initials(user.tenantName).slice(0, 2)}</span>}
      </div>
      <div>
        <strong title={user.tenantName}>{user.tenantName}</strong>
        <span>Institute workspace</span>
      </div>
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

const DASHBOARD_CHART_COLORS = ["#2563eb", "#0f766e", "#f59e0b", "#7c3aed", "#dc2626", "#64748b"];

function Dashboard({ dashboard, followUps = [], pipeline = [], loading, error, onRetry, onNewLead, canManageLeads }) {
  const now = Date.now();
  const totalLeads = Number(dashboard?.totalLeads || 0);
  const newLeadsToday = Number(dashboard?.newLeadsToday || 0);
  const contacted = Number(dashboard?.contacted || 0);
  const enrolled = Number(dashboard?.enrolled || 0);
  const openLeads = Math.max(totalLeads - enrolled, 0);
  const conversionRate = totalLeads > 0 ? Math.round((enrolled / totalLeads) * 100) : 0;
  const scheduledFollowUps = followUps.filter((item) => item.status === "Scheduled");
  const overdueFollowUps = scheduledFollowUps.filter((item) => item.dueAt && new Date(item.dueAt).getTime() < now).length;
  const highPriorityFollowUps = scheduledFollowUps.filter((item) => ["Urgent", "High"].includes(item.priority)).length;

  const pipelineData = useMemo(() => {
    return (pipeline || [])
      .filter((stage) => stage && typeof stage.count !== "undefined")
      .map((stage, index) => ({
        name: stage.name || `Stage ${index + 1}`,
        count: Number(stage.count || 0),
        fill: DASHBOARD_CHART_COLORS[index % DASHBOARD_CHART_COLORS.length],
      }));
  }, [pipeline]);

  const pipelineTotal = pipelineData.reduce((sum, item) => sum + item.count, 0);
  const pipelineWithLeads = pipelineData.filter((item) => item.count > 0);
  const progressData = [
    { name: "New", value: newLeadsToday },
    { name: "Contacted", value: contacted },
    { name: "Open", value: openLeads },
    { name: "Enrolled", value: enrolled },
  ];
  const focusItems = [
    {
      title: overdueFollowUps > 0 ? "Recover overdue follow-ups" : "Follow-up discipline is clean",
      description: overdueFollowUps > 0 ? `${overdueFollowUps} scheduled follow-up${overdueFollowUps === 1 ? "" : "s"} crossed the due time.` : "No overdue scheduled follow-ups in the current queue.",
      tone: overdueFollowUps > 0 ? "danger" : "success",
    },
    {
      title: highPriorityFollowUps > 0 ? "Call high intent students first" : "No high-priority queue pressure",
      description: highPriorityFollowUps > 0 ? `${highPriorityFollowUps} urgent/high priority task${highPriorityFollowUps === 1 ? "" : "s"} need attention.` : "Current queue has no urgent or high-priority follow-ups.",
      tone: highPriorityFollowUps > 0 ? "warning" : "neutral",
    },
    {
      title: conversionRate >= 20 ? "Enrollment conversion looks healthy" : "Improve stage movement",
      description: `${conversionRate}% of total leads are enrolled. Review stuck stages before adding more cold leads.`,
      tone: conversionRate >= 20 ? "success" : "neutral",
    },
  ];

  return (
    <>
      <section className="dashboard-hero">
        <div>
          <span className="eyebrow">Admissions command center</span>
          <h1>Counsellor Dashboard</h1>
          <p>Live view of admission workload, lead movement, and follow-up pressure.</p>
        </div>
        <div className="dashboard-hero-actions">
          <span className="dashboard-sync">{loading ? "Refreshing live data..." : "Live CRM data"}</span>
          <button className="primary-button" onClick={onNewLead} disabled={!canManageLeads}>
            <Plus size={18} />
            New Lead
          </button>
        </div>
      </section>

      <div className="metric-grid dashboard-metric-grid">
        <Metric title="Total Leads" value={formatNumber(totalLeads)} trend={`${conversionRate}% enrolled`} />
        <Metric title="New Today" value={formatNumber(newLeadsToday)} trend="Needs first touch" warning={newLeadsToday > 0} />
        <Metric title="Contacted" value={formatNumber(contacted)} trend="Active conversations" />
        <Metric title="Enrolled" value={formatNumber(enrolled)} trend="Converted students" />
      </div>

      <div className="dashboard-grid dashboard-command-grid">
        <Card title="Admission Pipeline" badge={`${formatNumber(pipelineTotal)} Leads`} className="wide-card dashboard-pipeline-card">
          {loading && <StatePanel title="Loading pipeline" message="Fetching live stage counts..." />}
          {error && <StatePanel title="Could not load pipeline" message={error} action={onRetry} />}
          {!loading && !error && pipelineData.length === 0 && <StatePanel title="No pipeline data" message="Pipeline stages will appear here once leads are available." />}
          {!loading && !error && (
            <div className="chart-shell">
              <ResponsiveContainer width="100%" height={238}>
                <BarChart data={pipelineData} margin={{ top: 24, right: 10, left: -24, bottom: 2 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                  <XAxis dataKey="name" tickLine={false} axisLine={false} tick={{ fill: "#64748b", fontSize: 12 }} interval={0} />
                  <YAxis tickLine={false} axisLine={false} tick={{ fill: "#94a3b8", fontSize: 12 }} allowDecimals={false} />
                  <Tooltip content={<ChartTooltip unit="leads" />} cursor={{ fill: "rgba(37, 99, 235, 0.06)" }} />
                  <Bar dataKey="count" radius={[0, 0, 0, 0]} barSize={44}>
                    {pipelineData.map((entry) => <Cell key={entry.name} fill={entry.fill} />)}
                    <LabelList dataKey="count" position="top" fill="#0f172a" fontSize={12} fontWeight={700} />
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>

        <Card title="Today's Schedule" badge={`${formatNumber(scheduledFollowUps.length)} Pending`} className="dashboard-focus-card">
          {loading && <StatePanel title="Loading follow-ups" message="Fetching the live queue..." />}
          {error && <StatePanel title="Could not load follow-ups" message={error} action={onRetry} />}
          {!loading && !error && scheduledFollowUps.length === 0 && <StatePanel title="No follow-ups" message="There are no scheduled follow-ups for this tenant." />}
          {!loading && !error && scheduledFollowUps.length > 0 && (
            <div className="schedule-list">
              {scheduledFollowUps.slice(0, 4).map((item) => (
                <FollowUpRow key={item.id} item={item} compact />
              ))}
            </div>
          )}
        </Card>

        <Card title="Admission Health" className="dashboard-chart-card">
          <div className="dashboard-health-grid">
            <div className="chart-shell pie-chart-shell">
              <ResponsiveContainer width="100%" height={230}>
                <PieChart>
                  <Tooltip content={<ChartTooltip unit="leads" />} />
                  <Pie data={pipelineWithLeads.length ? pipelineWithLeads : [{ name: "No leads", count: 1, fill: "#e2e8f0" }]} dataKey="count" nameKey="name" innerRadius={62} outerRadius={92} paddingAngle={3}>
                    {(pipelineWithLeads.length ? pipelineWithLeads : [{ name: "No leads", fill: "#e2e8f0" }]).map((entry) => <Cell key={entry.name} fill={entry.fill} />)}
                  </Pie>
                </PieChart>
              </ResponsiveContainer>
              <div className="chart-center-label">
                <strong>{conversionRate}%</strong>
                <span>conversion</span>
              </div>
            </div>
            <div className="pipeline-legend">
              {(pipelineWithLeads.length ? pipelineWithLeads : pipelineData.slice(0, 4)).slice(0, 5).map((item) => (
                <div key={item.name}>
                  <span style={{ background: item.fill }} />
                  <p>{item.name}</p>
                  <strong>{formatNumber(item.count)}</strong>
                </div>
              ))}
            </div>
          </div>
        </Card>

        <Card title="Lead Progress Snapshot" className="dashboard-chart-card">
          <div className="chart-shell">
            <ResponsiveContainer width="100%" height={230}>
              <AreaChart data={progressData} margin={{ top: 18, right: 12, left: -24, bottom: 0 }}>
                <defs>
                  <linearGradient id="leadProgressFill" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#2563eb" stopOpacity={0.28} />
                    <stop offset="95%" stopColor="#2563eb" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="name" tickLine={false} axisLine={false} tick={{ fill: "#64748b", fontSize: 12 }} />
                <YAxis tickLine={false} axisLine={false} tick={{ fill: "#94a3b8", fontSize: 12 }} allowDecimals={false} />
                <Tooltip content={<ChartTooltip unit="leads" />} />
                <Area type="monotone" dataKey="value" stroke="#2563eb" strokeWidth={3} fill="url(#leadProgressFill)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </Card>

        <Card title="Counsellor Focus" className="full-row dashboard-action-card">
          <div className="dashboard-action-list">
            {focusItems.map((item) => (
              <article className={`dashboard-action-item ${item.tone}`} key={item.title}>
                <span />
                <div>
                  <strong>{item.title}</strong>
                  <p>{item.description}</p>
                </div>
              </article>
            ))}
          </div>
        </Card>
      </div>
    </>
  );
}

function LeadsPage({
  leads,
  options,
  filters,
  loading,
  error,
  onRetry,
  onFiltersChange,
  onResetFilters,
  onNewLead,
  onImport,
  onExport,
  onOpenLead,
  onBulkAction,
  canManageLeads,
  canArchiveLeads,
  currentUser,
  canImportExportLeads,
  exportStatus,
}) {
  const items = leads?.items || [];
  const total = leads?.total || 0;
  const [selected, setSelected] = useState({});
  const [bulkAction, setBulkAction] = useState("");
  const [bulkTarget, setBulkTarget] = useState("");
  const [bulkStatus, setBulkStatus] = useState({ saving: false, error: "", message: "" });
  const selectionKey = `${filters.search}|${filters.branchId}|${filters.courseId}|${filters.sourceId}|${filters.stageId}|${filters.assignedUserId}|${filters.priority}|${filters.archive}|${filters.sort}|${filters.page}|${filters.pageSize}`;
  const selectedItems = items.filter((item) => selected[item.id]);

  useEffect(() => {
    setSelected({});
    setBulkAction("");
    setBulkTarget("");
    setBulkStatus({ saving: false, error: "", message: "" });
  }, [selectionKey]);

  const toggleLead = (lead) => {
    if (bulkStatus.saving) return;
    setSelected((current) => ({ ...current, [lead.id]: !current[lead.id] }));
    setBulkStatus((current) => ({ ...current, error: "", message: "" }));
  };

  const togglePage = () => {
    if (bulkStatus.saving) return;
    const shouldSelect = selectedItems.length !== items.length;
    setSelected(shouldSelect ? Object.fromEntries(items.map((item) => [item.id, true])) : {});
    setBulkStatus((current) => ({ ...current, error: "", message: "" }));
  };

  const applyBulkAction = async () => {
    if (selectedItems.length === 0 || !bulkAction || bulkStatus.saving) return;
    if ((bulkAction === "assign" || bulkAction === "changeStage") && !bulkTarget && bulkAction !== "assign") {
      setBulkStatus({ saving: false, error: "Select a target before applying this action.", message: "" });
      return;
    }

    const destructive = bulkAction === "archive" || bulkAction === "restore";
    if (destructive && !window.confirm(`${bulkAction === "archive" ? "Archive" : "Restore"} ${selectedItems.length} selected lead(s)?`)) return;

    setBulkStatus({ saving: true, error: "", message: "" });
    try {
      const payload = {
        action: bulkAction,
        items: selectedItems.map((item) => ({ leadId: item.id, version: item.version })),
        assignedUserId: bulkAction === "assign" ? (bulkTarget || null) : null,
        leadStageId: bulkAction === "changeStage" ? bulkTarget : null,
      };
      const response = await onBulkAction(payload);
      setSelected({});
      setBulkAction("");
      setBulkTarget("");
      setBulkStatus({ saving: false, error: "", message: response.message || `${response.updated} lead(s) updated.` });
    } catch (error) {
      setBulkStatus({ saving: false, error: error instanceof Error ? error.message : "Unable to update selected leads.", message: "" });
    }
  };

  const changeBulkAction = (value) => {
    setBulkAction(value);
    setBulkTarget("");
    setBulkStatus((current) => ({ ...current, error: "", message: "" }));
  };

  const counselorOptions = ["Counselor", "Telecaller"].includes(currentUser?.role)
    ? options.counselors.filter((item) => item.id === currentUser.userId)
    : options.counselors;
  const activeFilters = [
    filters.search,
    filters.branchId,
    filters.courseId,
    filters.sourceId,
    filters.stageId,
    filters.assignedUserId,
    filters.priority,
    filters.archive && filters.archive !== "active" ? filters.archive : "",
  ].filter(Boolean).length;

  return (
    <section className="leads-workspace">
      <div className="leads-hero">
        <div>
          <span className="eyebrow">Lead operations</span>
          <h1>Leads Management</h1>
          <p>Qualify enquiries, assign counsellors, move stages, and keep admission follow-ups clean.</p>
        </div>
        <div className="leads-hero-actions">
          <div className="leads-hero-stat">
            <span>Total leads</span>
            <strong>{loading ? "..." : formatNumber(total)}</strong>
          </div>
          <div className="leads-hero-stat">
            <span>Filters</span>
            <strong>{activeFilters}</strong>
          </div>
          <button className="primary-button" onClick={onNewLead} disabled={!canManageLeads}>
            <Plus size={18} />
            Add Lead
          </button>
          {canImportExportLeads && (
            <>
              <button className="secondary-button" onClick={onImport}>
                <FileText size={18} />
                Import
              </button>
              <button className="secondary-button" onClick={() => onExport("csv")} disabled={Boolean(exportStatus.exporting)}>
                <Download size={18} />
                {exportStatus.exporting === "csv" ? "Exporting..." : "CSV"}
              </button>
              <button className="secondary-button" onClick={() => onExport("xlsx")} disabled={Boolean(exportStatus.exporting)}>
                <Download size={18} />
                {exportStatus.exporting === "xlsx" ? "Exporting..." : "XLSX"}
              </button>
            </>
          )}
        </div>
      </div>
      {exportStatus.error && <div className="form-alert">{exportStatus.error}</div>}
      {bulkStatus.error && <div className="form-alert" role="alert">{bulkStatus.error}</div>}
      {bulkStatus.message && <div className="form-success" role="status">{bulkStatus.message}</div>}
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
        canSelect={canArchiveLeads}
        selected={selected}
        onToggleLead={toggleLead}
        onTogglePage={togglePage}
      />
      {canArchiveLeads && selectedItems.length > 0 && (
        <div className="bulk-action-bar" role="region" aria-label="Bulk lead actions">
          <strong>{selectedItems.length} selected</strong>
          <select value={bulkAction} onChange={(event) => changeBulkAction(event.target.value)} disabled={bulkStatus.saving} aria-label="Bulk action">
            <option value="">Choose action</option>
            <option value="assign">Assign counsellor</option>
            <option value="changeStage">Change stage</option>
            {canArchiveLeads && filters.archive !== "archived" && <option value="archive">Archive</option>}
            {canArchiveLeads && filters.archive !== "active" && <option value="restore">Restore</option>}
          </select>
          {bulkAction === "assign" && (
            <select value={bulkTarget} onChange={(event) => setBulkTarget(event.target.value)} disabled={bulkStatus.saving} aria-label="Counsellor">
              <option value="">Unassigned</option>
              {counselorOptions.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          )}
          {bulkAction === "changeStage" && (
            <select value={bulkTarget} onChange={(event) => setBulkTarget(event.target.value)} disabled={bulkStatus.saving} aria-label="Stage">
              <option value="">Select stage</option>
              {options.stages.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          )}
          <button className="primary-button" onClick={applyBulkAction} disabled={!bulkAction || (bulkAction === "changeStage" && !bulkTarget) || bulkStatus.saving}>
            {bulkStatus.saving ? "Updating..." : "Apply"}
          </button>
          <button className="ghost-button" onClick={() => setSelected({})} disabled={bulkStatus.saving}>Clear</button>
        </div>
      )}
    </section>
  );
}

function AddLeadModal({ options, initialValues = {}, saving, error, fieldErrors, onClose, onSubmit }) {
  const [form, setForm] = useState(() => createDefaultLeadForm(options, initialValues));
  const [clientErrors, setClientErrors] = useState({});
  const hasRequiredOptions = options.courses.length > 0 && options.sources.length > 0 && options.stages.length > 0;

  useEffect(() => {
    setForm(createDefaultLeadForm(options, initialValues));
    setClientErrors({});
  }, [options, initialValues]);

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

function LeadImportModal({ onClose, onImported }) {
  const [file, setFile] = useState(null);
  const [duplicateMode, setDuplicateMode] = useState("skip");
  const [mapping, setMapping] = useState({});
  const [preview, setPreview] = useState(null);
  const [previewStale, setPreviewStale] = useState(false);
  const [status, setStatus] = useState({
    loading: "",
    error: "",
    message: "",
  });

  const busy = Boolean(status.loading);
  const importableRows = (preview?.createRows || 0) + (preview?.updateRows || 0);
  const canCommit = preview && !previewStale && preview.errorCount === 0 && importableRows > 0 && !busy;

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === "Escape" && !busy) {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [busy, onClose]);

  const handleFileChange = (event) => {
    const selectedFile = event.target.files?.[0] || null;
    setFile(selectedFile);
    setMapping({});
    setPreview(null);
    setPreviewStale(false);
    setStatus({ loading: "", error: "", message: "" });
  };

  const handleDuplicateModeChange = (value) => {
    setDuplicateMode(value);
    if (preview) {
      setPreviewStale(true);
    }
  };

  const updateMapping = (key, value) => {
    setMapping((current) => {
      const next = { ...current };
      if (value) {
        next[key] = value;
      } else {
        delete next[key];
      }
      return next;
    });
    if (preview) {
      setPreviewStale(true);
    }
  };

  const downloadTemplate = async (format) => {
    setStatus({ loading: `template-${format}`, error: "", message: "" });
    try {
      const response = await downloadLeadImportTemplate(format);
      downloadFileResponse(response, `lead-import-template.${format}`);
      setStatus({ loading: "", error: "", message: "" });
    } catch (error) {
      setStatus({
        loading: "",
        error: error instanceof Error ? error.message : "Unable to download import template.",
        message: "",
      });
    }
  };

  const runPreview = async () => {
    if (!file) {
      setStatus({ loading: "", error: "Select a CSV or XLSX file first.", message: "" });
      return;
    }

    setStatus({ loading: "preview", error: "", message: "" });
    try {
      const response = await previewLeadImport({ file, mapping, duplicateMode });
      setPreview(response);
      setMapping(response.mapping || {});
      setPreviewStale(false);
      setStatus({
        loading: "",
        error: "",
        message: response.errorCount > 0 ? "Preview completed with errors." : "Preview completed.",
      });
    } catch (error) {
      setStatus({
        loading: "",
        error: error instanceof Error ? error.message : "Unable to preview import file.",
        message: "",
      });
    }
  };

  const commitImport = async () => {
    if (!canCommit) {
      return;
    }

    setStatus({ loading: "commit", error: "", message: "" });
    try {
      const response = await commitLeadImport({
        file,
        mapping,
        duplicateMode,
        fingerprint: preview.fingerprint,
      });
      setStatus({ loading: "", error: "", message: response.message || "Lead import completed." });
      await onImported();
    } catch (error) {
      if (error?.data?.preview) {
        setPreview(error.data.preview);
        setMapping(error.data.preview.mapping || {});
        setPreviewStale(false);
      }
      setStatus({
        loading: "",
        error: error instanceof Error ? error.message : "Unable to commit lead import.",
        message: "",
      });
    }
  };

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !busy && onClose()}>
      <section className="modal wide-modal" role="dialog" aria-modal="true" aria-labelledby="lead-import-title">
        <header className="modal-header">
          <div>
            <h2 id="lead-import-title">Import Leads</h2>
            <p>Upload CSV or XLSX files, preview validation results, then commit the clean rows.</p>
          </div>
          <button className="icon-button" onClick={onClose} disabled={busy} aria-label="Close lead import">
            <X size={20} />
          </button>
        </header>

        {status.error && <div className="form-alert">{status.error}</div>}
        {status.message && <div className={preview?.errorCount ? "form-alert" : "form-success"}>{status.message}</div>}

        <div className="import-toolbar">
          <Field label="Lead File" required>
            <input type="file" accept=".csv,.xlsx" onChange={handleFileChange} disabled={busy} />
          </Field>
          <Field label="Existing Phone">
            <select value={duplicateMode} onChange={(event) => handleDuplicateModeChange(event.target.value)} disabled={busy}>
              <option value="skip">Skip existing leads</option>
              <option value="update">Update existing leads</option>
            </select>
          </Field>
          <div className="template-actions" aria-label="Download import template">
            <button type="button" className="secondary-button" onClick={() => downloadTemplate("xlsx")} disabled={busy}>
              <Download size={18} />
              Template XLSX
            </button>
            <button type="button" className="secondary-button" onClick={() => downloadTemplate("csv")} disabled={busy}>
              <Download size={18} />
              Template CSV
            </button>
          </div>
        </div>

        {preview && (
          <>
            <div className="import-summary">
              <MetricPill label="Rows" value={preview.totalRows} />
              <MetricPill label="Create" value={preview.createRows} />
              <MetricPill label="Update" value={preview.updateRows} />
              <MetricPill label="Skip" value={preview.skipRows} />
              <MetricPill label="Errors" value={preview.errorCount} tone={preview.errorCount ? "danger" : ""} />
              <MetricPill label="Warnings" value={preview.warningCount} tone={preview.warningCount ? "warn" : ""} />
            </div>

            <div className="mapping-panel">
              <div className="section-heading">
                <h3>Column Mapping</h3>
                {previewStale && <span className="inline-warning">Preview again after mapping changes.</span>}
              </div>
              <div className="mapping-grid">
                {preview.columns.map((column) => (
                  <label key={column.key} className="mapping-row">
                    <span>
                      {column.label}
                      {column.required && <em>Required</em>}
                    </span>
                    <select value={mapping[column.key] || ""} onChange={(event) => updateMapping(column.key, event.target.value)} disabled={busy}>
                      <option value="">Not mapped</option>
                      {preview.headers.map((header) => (
                        <option key={header} value={header}>{header}</option>
                      ))}
                    </select>
                  </label>
                ))}
              </div>
            </div>

            {preview.issues.length > 0 && (
              <div className="issues-panel">
                <div className="section-heading">
                  <h3>Validation Issues</h3>
                  {preview.issuesTruncated && <span className="inline-warning">Showing first 500 issues.</span>}
                </div>
                <div className="issues-list">
                  {preview.issues.slice(0, 12).map((issue, index) => (
                    <div className={`issue-row ${issue.severity}`} key={`${issue.rowNumber}-${issue.field}-${index}`}>
                      <strong>{issue.rowNumber ? `Row ${issue.rowNumber}` : "File"}</strong>
                      <span>{issue.field}</span>
                      <p>{issue.message}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="import-preview-table">
              <table>
                <thead>
                  <tr>
                    <th>Row</th>
                    <th>Action</th>
                    <th>Student</th>
                    <th>Email</th>
                    <th>Phone</th>
                    <th>Issues</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((row) => (
                    <tr key={row.rowNumber}>
                      <td>{row.rowNumber}</td>
                      <td><Badge label={row.action} muted={row.action === "skip"} /></td>
                      <td>{mappedPreviewValue(row, preview.mapping, "studentName") || "-"}</td>
                      <td>{mappedPreviewValue(row, preview.mapping, "email") || "-"}</td>
                      <td>{mappedPreviewValue(row, preview.mapping, "phone") || "-"}</td>
                      <td>{row.issues.length ? `${row.issues.length} issue${row.issues.length === 1 ? "" : "s"}` : "Clean"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}

        <footer className="modal-actions">
          <button type="button" className="ghost-button" onClick={onClose} disabled={busy}>Close</button>
          <button type="button" className="secondary-button" onClick={runPreview} disabled={busy || !file}>
            <RotateCcw size={18} />
            {status.loading === "preview" ? "Previewing..." : preview ? "Preview Again" : "Preview"}
          </button>
          <button type="button" className="primary-button" onClick={commitImport} disabled={!canCommit}>
            <CheckCircle2 size={18} />
            {status.loading === "commit" ? "Importing..." : "Commit Import"}
          </button>
        </footer>
      </section>
    </div>
  );
}

function MetricPill({ label, value, tone = "" }) {
  return (
    <div className={`metric-pill ${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function LeadDetailDrawer({
  leadId,
  lead,
  options,
  communicationTemplates,
  loading,
  error,
  actionStatus,
  currentUser,
  onClose,
  onRetry,
  onUpdate,
  onArchive,
  onRestore,
  onAddActivity,
  onApplyTemplate,
  onCreateFollowUp,
  onRescheduleFollowUp,
  onCancelFollowUp,
  onCompleteFollowUp,
  onUploadDocument,
  onVerifyDocument,
  onRejectDocument,
  onDeleteDocument,
  onDownloadDocument,
  onCreatePayment,
  onUpdatePayment,
  onAddPaymentTransaction,
  onCancelPayment,
  onCreateApplication,
  canManageLeads,
  canArchiveLeads,
  canManagePayments,
}) {
  const [editForm, setEditForm] = useState(() => createLeadUpdateForm(lead));
  const [noteForm, setNoteForm] = useState({ type: "Note", description: "" });
  const [templateForm, setTemplateForm] = useState({ templateId: "", note: "" });
  const [followUpForm, setFollowUpForm] = useState(() => createDefaultFollowUpForm(lead));
  const [rescheduleForm, setRescheduleForm] = useState(null);
  const [documentForms, setDocumentForms] = useState({});
  const [paymentForm, setPaymentForm] = useState(createDefaultPaymentForm());
  const [paymentTransactionForms, setPaymentTransactionForms] = useState({});
  const [editingPaymentId, setEditingPaymentId] = useState("");
  const [editingPaymentForm, setEditingPaymentForm] = useState(null);

  useEffect(() => {
    setEditForm(createLeadUpdateForm(lead));
    setFollowUpForm(createDefaultFollowUpForm(lead));
    setTemplateForm((current) => ({
      templateId: communicationTemplates.find((item) => item.isActive)?.id || current.templateId || "",
      note: "",
    }));
    setRescheduleForm(null);
    setDocumentForms({});
    setPaymentForm(createDefaultPaymentForm());
    setPaymentTransactionForms({});
    setEditingPaymentId("");
    setEditingPaymentForm(null);
  }, [lead, communicationTemplates]);

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
  const saving = actionStatus.saving;
  const canEditProfile = canManageLeads && !archived && !saving;
  const canUseEngagement = canManageLeads && !archived && !saving;
  const canUploadDocuments = canManageLeads && !archived && !saving;
  const canReviewDocuments = ["Owner", "Admin", "BranchManager"].includes(currentUser?.role) && !archived && !saving;
  const canManageLeadPayments = canManagePayments && !archived && !saving;
  const hasProfileChanges = lead ? !areLeadProfileFormsEqual(editForm, createLeadUpdateForm(lead)) : false;
  const assigneeLockedToSelf = ["Counselor", "Telecaller"].includes(currentUser?.role);
  const profileBlockedMessage = archived
    ? "Restore this lead before editing profile, activities, or follow-ups."
    : !canManageLeads
      ? "Read-only users cannot make changes."
      : !canSave
        ? "Complete the required profile fields before saving."
        : "";
  const stageOptions = includeCurrentOption(options.stages, lead?.leadStageId, lead?.stage);
  const courseOptions = includeCurrentOption(options.courses, lead?.courseId, lead?.course);
  const sourceOptions = includeCurrentOption(options.sources, lead?.leadSourceId, lead?.source);
  const branchOptions = includeCurrentOption(options.branches, lead?.branchId, lead?.branch);
  const counselorOptions = includeCurrentOption(options.counselors, lead?.assignedUserId, lead?.counselor === "Unassigned" ? "" : lead?.counselor);
  const activeTemplates = communicationTemplates.filter((item) => item.isActive);
  const documents = lead?.documents || [];
  const payments = lead?.payments || [];

  const handleUpdateSubmit = async (event) => {
    event.preventDefault();
    if (!canEditProfile || !canSave) {
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
    if (!canUseEngagement || !noteForm.description.trim()) {
      return;
    }

    const updatedLead = await onAddActivity({
      type: noteForm.type,
      description: noteForm.description.trim(),
    });
    if (updatedLead) {
      setNoteForm({ type: "Note", description: "" });
    }
  };

  const handleFollowUpSubmit = async (event) => {
    event.preventDefault();
    if (!canUseEngagement || !followUpForm.dueAt) {
      return;
    }

    const updatedLead = await onCreateFollowUp({
      type: followUpForm.type,
      priority: followUpForm.priority,
      assignedUserId: optionalValue(followUpForm.assignedUserId),
      dueAt: new Date(followUpForm.dueAt).toISOString(),
    });
    if (updatedLead) {
      setFollowUpForm(createDefaultFollowUpForm(updatedLead));
    }
  };

  const handleTemplateSubmit = async (event) => {
    event.preventDefault();
    if (!canUseEngagement || !templateForm.templateId) {
      return;
    }

    const updatedLead = await onApplyTemplate({
      templateId: templateForm.templateId,
      note: optionalValue(templateForm.note),
    });
    if (updatedLead) {
      setTemplateForm((current) => ({ ...current, note: "" }));
    }
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
    if (!canUseEngagement || !rescheduleForm?.id || !rescheduleForm.dueAt) {
      return;
    }

    const updatedLead = await onRescheduleFollowUp(rescheduleForm.id, {
      type: rescheduleForm.type,
      priority: rescheduleForm.priority,
      assignedUserId: optionalValue(rescheduleForm.assignedUserId),
      dueAt: new Date(rescheduleForm.dueAt).toISOString(),
      version: rescheduleForm.version,
    });
    if (updatedLead) {
      setRescheduleForm(null);
    }
  };

  const updateDocumentForm = (documentTypeId, updates) => {
    setDocumentForms((current) => ({
      ...current,
      [documentTypeId]: {
        file: null,
        notes: "",
        rejectNotes: "",
        ...(current[documentTypeId] || {}),
        ...updates,
      },
    }));
  };

  const handleDocumentUpload = async (event, document) => {
    event.preventDefault();
    const form = documentForms[document.documentTypeId] || {};
    if (!canUploadDocuments || !form.file) {
      return;
    }

    const updatedLead = await onUploadDocument({
      documentTypeId: document.documentTypeId,
      file: form.file,
      version: document.version,
      notes: optionalValue(form.notes),
    });
    if (updatedLead) {
      updateDocumentForm(document.documentTypeId, { file: null, notes: "" });
    }
  };

  const handleDocumentReject = async (document) => {
    const notes = documentForms[document.documentTypeId]?.rejectNotes || "";
    if (!canReviewDocuments || !document.documentId || !notes.trim()) {
      return;
    }

    const updatedLead = await onRejectDocument(document, notes.trim());
    if (updatedLead) {
      updateDocumentForm(document.documentTypeId, { rejectNotes: "" });
    }
  };

  const handlePaymentCreate = async (event) => {
    event.preventDefault();
    if (!canManageLeadPayments || !paymentForm.title.trim() || !paymentForm.amountDue) {
      return;
    }

    const updatedLead = await onCreatePayment({
      title: paymentForm.title.trim(),
      amountDue: Number(paymentForm.amountDue),
      currency: "INR",
      dueDate: paymentForm.dueDate ? new Date(`${paymentForm.dueDate}T00:00:00`).toISOString() : null,
      notes: optionalValue(paymentForm.notes),
      version: 0,
    });
    if (updatedLead) {
      setPaymentForm(createDefaultPaymentForm());
    }
  };

  const openPaymentEdit = (payment) => {
    setEditingPaymentId(payment.id);
    setEditingPaymentForm({
      title: payment.title,
      amountDue: String(payment.amountDue),
      dueDate: payment.dueDate ? toDateInputValue(new Date(payment.dueDate)) : "",
      notes: payment.notes || "",
    });
  };

  const handlePaymentUpdate = async (event) => {
    event.preventDefault();
    const payment = payments.find((item) => item.id === editingPaymentId);
    if (!canManageLeadPayments || !payment || !editingPaymentForm?.title.trim() || !editingPaymentForm.amountDue) {
      return;
    }

    const updatedLead = await onUpdatePayment(payment, {
      title: editingPaymentForm.title.trim(),
      amountDue: Number(editingPaymentForm.amountDue),
      currency: "INR",
      dueDate: editingPaymentForm.dueDate ? new Date(`${editingPaymentForm.dueDate}T00:00:00`).toISOString() : null,
      notes: optionalValue(editingPaymentForm.notes),
      version: payment.version,
    });
    if (updatedLead) {
      setEditingPaymentId("");
      setEditingPaymentForm(null);
    }
  };

  const updatePaymentTransactionForm = (paymentId, updates) => {
    setPaymentTransactionForms((current) => ({
      ...current,
      [paymentId]: {
        amount: "",
        method: "UPI",
        referenceNumber: "",
        receiptNumber: "",
        paidAt: "",
        notes: "",
        ...(current[paymentId] || {}),
        ...updates,
      },
    }));
  };

  const handlePaymentTransactionCreate = async (event, payment) => {
    event.preventDefault();
    const form = paymentTransactionForms[payment.id] || {};
    if (!canManageLeadPayments || !form.amount || Number(form.amount) <= 0) {
      return;
    }

    const updatedLead = await onAddPaymentTransaction(payment, {
      amount: Number(form.amount),
      method: form.method || "UPI",
      referenceNumber: optionalValue(form.referenceNumber),
      receiptNumber: optionalValue(form.receiptNumber),
      paidAt: form.paidAt ? new Date(form.paidAt).toISOString() : null,
      notes: optionalValue(form.notes),
      version: payment.version,
    });
    if (updatedLead) {
      updatePaymentTransactionForm(payment.id, {
        amount: "",
        referenceNumber: "",
        receiptNumber: "",
        paidAt: "",
        notes: "",
      });
    }
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
                  {canArchiveLeads && archived && (
                    <button type="button" className="ghost-button" disabled={saving} onClick={() => onRestore({ version: lead.version })}>
                      <RotateCcw size={16} />
                      Restore Lead
                    </button>
                  )}
                  {canArchiveLeads && !archived && (
                    <button type="button" className="ghost-button danger-text" disabled={saving} onClick={() => onArchive({ version: lead.version })}>
                      <UserX size={16} />
                      Archive Lead
                    </button>
                  )}
                  <button type="submit" className="primary-button" disabled={!canEditProfile || !canSave || !hasProfileChanges}>
                    {saving ? "Saving..." : "Save Lead Profile"}
                  </button>
                </div>
              </div>
              {profileBlockedMessage && <p className="drawer-permission-note">{profileBlockedMessage}</p>}
              {!profileBlockedMessage && !hasProfileChanges && <p className="drawer-permission-note">No unsaved profile changes.</p>}
              {!canArchiveLeads && <p className="drawer-permission-note">Archive and restore actions are limited to owner, admin, and branch manager roles.</p>}
              <div className="form-grid compact">
                <Field label="Student Name" error={getFieldError("studentName")} required>
                  <input value={editForm.studentName} maxLength={160} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, studentName: event.target.value }))} required />
                </Field>
                <Field label="Guardian Name" error={getFieldError("guardianName")}>
                  <input value={editForm.guardianName} maxLength={160} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, guardianName: event.target.value }))} />
                </Field>
                <Field label="Email" error={getFieldError("email")} required>
                  <input type="email" value={editForm.email} maxLength={240} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, email: event.target.value }))} required />
                </Field>
                <Field label="Phone" error={getFieldError("phone")} required>
                  <input value={editForm.phone} maxLength={40} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, phone: event.target.value }))} required />
                </Field>
                <Field label="City" error={getFieldError("city")}>
                  <input value={editForm.city} maxLength={120} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, city: event.target.value }))} />
                </Field>
                <Field label="Course" error={getFieldError("courseId")} required>
                  <select value={editForm.courseId} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, courseId: event.target.value }))} required>
                    {courseOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Source" error={getFieldError("leadSourceId")} required>
                  <select value={editForm.leadSourceId} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, leadSourceId: event.target.value }))} required>
                    {sourceOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Stage" error={getFieldError("leadStageId")} required>
                  <select value={editForm.leadStageId} disabled={!canEditProfile} onChange={(event) => {
                    const nextStageId = event.target.value;
                    setEditForm((current) => ({ ...current, leadStageId: nextStageId }));
                  }} required>
                    {stageOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Branch" error={getFieldError("branchId")}>
                  <select value={editForm.branchId} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, branchId: event.target.value }))}>
                    <option value="">No branch</option>
                    {branchOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                </Field>
                <Field label="Status" error={getFieldError("status")}>
                  <select value={editForm.status} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, status: event.target.value }))}>
                    {["New Lead", "Interested", "Follow Up", "Enrolled", "Dropped"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Priority" error={getFieldError("priority")}>
                  <select value={editForm.priority} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, priority: event.target.value }))}>
                    {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                  <select value={editForm.assignedUserId} disabled={!canEditProfile || assigneeLockedToSelf} onChange={(event) => {
                    const assignedUserId = event.target.value;
                    setEditForm((current) => ({ ...current, assignedUserId }));
                  }}>
                    <option value="">Unassigned</option>
                    {counselorOptions.map((item) => <option key={item.id} value={item.id} disabled={item.inactive}>{item.name}{item.inactive ? " (inactive)" : ""}</option>)}
                  </select>
                  {assigneeLockedToSelf && <small className="field-hint">Counsellors can only keep leads assigned to themselves.</small>}
                </Field>
                <Field label="Next Follow-up" error={getFieldError("nextFollowUpAt")} className="span-2">
                  <input type="datetime-local" value={editForm.nextFollowUpAt} disabled={!canEditProfile} onChange={(event) => setEditForm((current) => ({ ...current, nextFollowUpAt: event.target.value }))} />
                </Field>
              </div>
            </form>

            <form className="drawer-section" onSubmit={handleTemplateSubmit}>
              <div className="section-heading">
                <h3>Use Communication Template</h3>
                <button type="submit" className="primary-button" disabled={!canUseEngagement || !templateForm.templateId}>
                  {saving ? "Saving..." : "Apply Template"}
                </button>
              </div>
              {profileBlockedMessage && <p className="drawer-permission-note">{profileBlockedMessage}</p>}
              {activeTemplates.length === 0 && <p className="drawer-permission-note">No active communication templates are available. Owners and admins can add templates from Settings.</p>}
              <div className="form-grid compact">
                <Field label="Template" error={getFieldError("templateId")} className="span-2" required>
                  <select value={templateForm.templateId} disabled={!canUseEngagement || activeTemplates.length === 0} onChange={(event) => setTemplateForm((current) => ({ ...current, templateId: event.target.value }))} required>
                    <option value="">Select template</option>
                    {activeTemplates.map((item) => (
                      <option key={item.id} value={item.id}>{item.channel} - {item.name}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Internal Note" error={getFieldError("note")} className="span-2">
                  <textarea value={templateForm.note} maxLength={500} disabled={!canUseEngagement || activeTemplates.length === 0} placeholder="Optional note stored with the rendered template activity." onChange={(event) => setTemplateForm((current) => ({ ...current, note: event.target.value }))} />
                </Field>
              </div>
              {templateForm.templateId && (
                <div className="template-preview">
                  <span>Template body</span>
                  <p>{activeTemplates.find((item) => item.id === templateForm.templateId)?.body || ""}</p>
                </div>
              )}
            </form>

            <form className="drawer-section" onSubmit={handleNoteSubmit}>
              <div className="section-heading">
                <h3>Add Activity</h3>
                <button type="submit" className="primary-button" disabled={!canUseEngagement || !noteForm.description.trim()}>
                  {saving ? "Saving..." : "Add Activity Note"}
                </button>
              </div>
              {profileBlockedMessage && <p className="drawer-permission-note">{profileBlockedMessage}</p>}
              <div className="form-grid compact">
                <Field label="Type" error={getFieldError("type")}>
                  <select value={noteForm.type} disabled={!canUseEngagement} onChange={(event) => setNoteForm((current) => ({ ...current, type: event.target.value }))}>
                    {["Note", "Call", "WhatsApp", "Email", "Meeting"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Description" error={getFieldError("description")} className="span-2" required>
                  <textarea value={noteForm.description} maxLength={500} disabled={!canUseEngagement} onChange={(event) => setNoteForm((current) => ({ ...current, description: event.target.value }))} required />
                </Field>
              </div>
            </form>

            <form className="drawer-section" onSubmit={handleFollowUpSubmit}>
              <div className="section-heading">
                <h3>Schedule Follow-up</h3>
                <button type="submit" className="primary-button" disabled={!canUseEngagement || !followUpForm.dueAt}>
                  {saving ? "Saving..." : "Schedule Follow-up"}
                </button>
              </div>
              {profileBlockedMessage && <p className="drawer-permission-note">{profileBlockedMessage}</p>}
              <div className="form-grid compact">
                <Field label="Type" error={getFieldError("type")}>
                  <select value={followUpForm.type} disabled={!canUseEngagement} onChange={(event) => setFollowUpForm((current) => ({ ...current, type: event.target.value }))}>
                    {["Call", "WhatsApp", "Email", "Walk-in"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Priority" error={getFieldError("priority")}>
                  <select value={followUpForm.priority} disabled={!canUseEngagement} onChange={(event) => setFollowUpForm((current) => ({ ...current, priority: event.target.value }))}>
                    {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                  <select value={followUpForm.assignedUserId} disabled={!canUseEngagement || assigneeLockedToSelf} onChange={(event) => setFollowUpForm((current) => ({ ...current, assignedUserId: event.target.value }))}>
                    <option value="">Current owner</option>
                    {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                  </select>
                  {assigneeLockedToSelf && <small className="field-hint">Counsellors can schedule follow-ups for their assigned lead owner.</small>}
                </Field>
                <Field label="Due At" error={getFieldError("dueAt")} required>
                  <input type="datetime-local" value={followUpForm.dueAt} disabled={!canUseEngagement} onChange={(event) => setFollowUpForm((current) => ({ ...current, dueAt: event.target.value }))} required />
                </Field>
              </div>
            </form>

            <section className="drawer-section">
              <div className="section-heading">
                <h3>Documents</h3>
                <span>{documents.filter((item) => item.documentId).length}/{documents.length}</span>
              </div>
              {archived && <p className="drawer-permission-note">Restore this lead before uploading or reviewing documents.</p>}
              {!canManageLeads && <p className="drawer-permission-note">Read-only users can view and download uploaded documents only.</p>}
              <div className="document-list">
                {documents.length === 0 && <p className="muted-text">No document checklist is configured.</p>}
                {documents.map((document) => {
                  const form = documentForms[document.documentTypeId] || {};
                  const hasFile = Boolean(document.documentId);
                  const canReplaceThisDocument = canUploadDocuments && (!hasFile || document.status !== "Verified" || canReviewDocuments);

                  return (
                    <div className="document-row" key={document.documentTypeId}>
                      <div className="document-main">
                        <div className="document-title-row">
                          <strong>{document.name}</strong>
                          <Badge label={document.isRequired ? "Required" : "Optional"} muted={!document.isRequired} />
                          <Badge label={document.status} muted={document.status === "Pending"} />
                        </div>
                        {hasFile ? (
                          <p>{document.fileName} - {formatFileSize(document.fileSizeBytes)}</p>
                        ) : (
                          <p>No file uploaded.</p>
                        )}
                        {document.notes && <small>{document.notes}</small>}
                        {document.uploadedAt && <small>Uploaded {formatFollowUpLabel(document.uploadedAt)}{document.uploadedBy ? ` by ${document.uploadedBy}` : ""}</small>}
                        {document.reviewedAt && <small>Reviewed {formatFollowUpLabel(document.reviewedAt)}{document.reviewedBy ? ` by ${document.reviewedBy}` : ""}</small>}
                      </div>
                      <div className="document-actions">
                        {hasFile && document.canDownload && (
                          <button type="button" className="ghost-button" disabled={saving} onClick={() => onDownloadDocument(document)}>
                            <Download size={16} />
                            Download
                          </button>
                        )}
                        {canReviewDocuments && hasFile && document.status !== "Verified" && (
                          <button type="button" className="ghost-button" disabled={saving} onClick={() => onVerifyDocument(document)}>
                            <CheckCircle2 size={16} />
                            Verify
                          </button>
                        )}
                        {canReviewDocuments && hasFile && (
                          <button type="button" className="ghost-button danger-text" disabled={saving || !form.rejectNotes?.trim()} onClick={() => handleDocumentReject(document)}>
                            <X size={16} />
                            Reject
                          </button>
                        )}
                        {canReviewDocuments && hasFile && document.status !== "Verified" && (
                          <button type="button" className="ghost-button danger-text" disabled={saving} onClick={() => onDeleteDocument(document)}>
                            <UserX size={16} />
                            Delete
                          </button>
                        )}
                      </div>
                      {canReviewDocuments && hasFile && (
                        <Field label="Review Notes" className="span-2" error={getFieldError("notes")}>
                          <input value={form.rejectNotes || ""} maxLength={500} disabled={saving} placeholder="Required when rejecting" onChange={(event) => updateDocumentForm(document.documentTypeId, { rejectNotes: event.target.value })} />
                        </Field>
                      )}
                      <form className="document-upload-form" onSubmit={(event) => handleDocumentUpload(event, document)}>
                        <Field label={hasFile ? "Replacement File" : "Upload File"} error={getFieldError("file")}>
                          <input type="file" accept=".pdf,.jpg,.jpeg,.png,.doc,.docx" disabled={!canReplaceThisDocument} onChange={(event) => updateDocumentForm(document.documentTypeId, { file: event.target.files?.[0] || null })} />
                        </Field>
                        <Field label="Upload Notes" error={getFieldError("notes")}>
                          <input value={form.notes || ""} maxLength={500} disabled={!canReplaceThisDocument} onChange={(event) => updateDocumentForm(document.documentTypeId, { notes: event.target.value })} />
                        </Field>
                        <button type="submit" className="primary-button" disabled={!canReplaceThisDocument || !form.file}>
                          {saving ? "Saving..." : hasFile ? "Replace" : "Upload"}
                        </button>
                      </form>
                      {!canReplaceThisDocument && canUploadDocuments && document.status === "Verified" && (
                        <p className="drawer-permission-note">Only reviewers can replace verified documents.</p>
                      )}
                    </div>
                  );
                })}
              </div>
            </section>

            <section className="drawer-section">
              <div className="section-heading">
                <h3>Payments</h3>
                <span>{formatCurrency(payments.reduce((total, item) => total + Number(item.balance || 0), 0), "INR")}</span>
              </div>
              {archived && <p className="drawer-permission-note">Restore this lead before adding or updating payments.</p>}
              {!canManagePayments && <p className="drawer-permission-note">Payment management is limited to owner, admin, and accountant roles.</p>}
              {canManageLeadPayments && (
                <form className="payment-form" onSubmit={handlePaymentCreate}>
                  <Field label="Fee Item" error={getFieldError("title")} required>
                    <input value={paymentForm.title} maxLength={160} onChange={(event) => setPaymentForm((current) => ({ ...current, title: event.target.value }))} required />
                  </Field>
                  <Field label="Amount Due" error={getFieldError("amountDue")} required>
                    <input type="number" min="0.01" step="0.01" value={paymentForm.amountDue} onChange={(event) => setPaymentForm((current) => ({ ...current, amountDue: event.target.value }))} required />
                  </Field>
                  <Field label="Due Date" error={getFieldError("dueDate")}>
                    <input type="date" value={paymentForm.dueDate} onChange={(event) => setPaymentForm((current) => ({ ...current, dueDate: event.target.value }))} />
                  </Field>
                  <Field label="Notes" error={getFieldError("notes")}>
                    <input value={paymentForm.notes} maxLength={500} onChange={(event) => setPaymentForm((current) => ({ ...current, notes: event.target.value }))} />
                  </Field>
                  <button type="submit" className="primary-button" disabled={!paymentForm.title.trim() || !paymentForm.amountDue}>
                    {saving ? "Saving..." : "Add Fee"}
                  </button>
                </form>
              )}
              <div className="payment-list">
                {payments.length === 0 && <p className="muted-text">No payment items added.</p>}
                {payments.map((payment) => {
                  const transactionForm = paymentTransactionForms[payment.id] || {};
                  const editable = canManageLeadPayments && payment.status !== "Cancelled";
                  const canReceive = editable && payment.balance > 0;
                  const canCancel = editable && payment.amountPaid <= 0;
                  return (
                    <div className="payment-row" key={payment.id}>
                      <div className="payment-main">
                        <div className="document-title-row">
                          <strong>{payment.title}</strong>
                          <Badge label={payment.status} muted={payment.status === "Pending"} />
                        </div>
                        <div className="payment-amount-grid">
                          <InfoItem label="Due" value={formatCurrency(payment.amountDue, payment.currency)} />
                          <InfoItem label="Paid" value={formatCurrency(payment.amountPaid, payment.currency)} />
                          <InfoItem label="Balance" value={formatCurrency(payment.balance, payment.currency)} />
                        </div>
                        {payment.dueDate && <small>Due {formatDate(payment.dueDate)}</small>}
                        {payment.notes && <small>{payment.notes}</small>}
                      </div>
                      <div className="document-actions">
                        {editable && (
                          <button type="button" className="ghost-button" disabled={saving} onClick={() => openPaymentEdit(payment)}>
                            <Pencil size={16} />
                            Edit
                          </button>
                        )}
                        {canCancel && (
                          <button type="button" className="ghost-button danger-text" disabled={saving} onClick={() => onCancelPayment(payment)}>
                            <X size={16} />
                            Cancel
                          </button>
                        )}
                      </div>
                      {editingPaymentId === payment.id && editingPaymentForm && (
                        <form className="payment-form span-all" onSubmit={handlePaymentUpdate}>
                          <Field label="Fee Item" error={getFieldError("title")} required>
                            <input value={editingPaymentForm.title} maxLength={160} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, title: event.target.value }))} required />
                          </Field>
                          <Field label="Amount Due" error={getFieldError("amountDue")} required>
                            <input type="number" min={payment.amountPaid || 0.01} step="0.01" value={editingPaymentForm.amountDue} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, amountDue: event.target.value }))} required />
                          </Field>
                          <Field label="Due Date" error={getFieldError("dueDate")}>
                            <input type="date" value={editingPaymentForm.dueDate} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, dueDate: event.target.value }))} />
                          </Field>
                          <Field label="Notes" error={getFieldError("notes")}>
                            <input value={editingPaymentForm.notes} maxLength={500} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, notes: event.target.value }))} />
                          </Field>
                          <div className="inline-editor-actions">
                            <button type="button" className="ghost-button" disabled={saving} onClick={() => { setEditingPaymentId(""); setEditingPaymentForm(null); }}>Cancel</button>
                            <button type="submit" className="primary-button" disabled={saving}>Save</button>
                          </div>
                        </form>
                      )}
                      {canReceive && (
                        <form className="payment-transaction-form span-all" onSubmit={(event) => handlePaymentTransactionCreate(event, payment)}>
                          <Field label="Receive Amount" error={getFieldError("amount")} required>
                            <input type="number" min="0.01" max={payment.balance} step="0.01" value={transactionForm.amount || ""} onChange={(event) => updatePaymentTransactionForm(payment.id, { amount: event.target.value })} required />
                          </Field>
                          <Field label="Method" error={getFieldError("method")}>
                            <select value={transactionForm.method || "UPI"} onChange={(event) => updatePaymentTransactionForm(payment.id, { method: event.target.value })}>
                              {["UPI", "Cash", "Bank Transfer", "Card", "Cheque", "Other"].map((item) => <option key={item} value={item}>{item}</option>)}
                            </select>
                          </Field>
                          <Field label="Paid At" error={getFieldError("paidAt")}>
                            <input type="datetime-local" value={transactionForm.paidAt || ""} onChange={(event) => updatePaymentTransactionForm(payment.id, { paidAt: event.target.value })} />
                          </Field>
                          <Field label="Reference" error={getFieldError("referenceNumber")}>
                            <input value={transactionForm.referenceNumber || ""} maxLength={120} onChange={(event) => updatePaymentTransactionForm(payment.id, { referenceNumber: event.target.value })} />
                          </Field>
                          <Field label="Receipt" error={getFieldError("receiptNumber")}>
                            <input value={transactionForm.receiptNumber || ""} maxLength={120} placeholder="Auto-generated if blank" onChange={(event) => updatePaymentTransactionForm(payment.id, { receiptNumber: event.target.value })} />
                          </Field>
                          <Field label="Notes" error={getFieldError("notes")}>
                            <input value={transactionForm.notes || ""} maxLength={500} onChange={(event) => updatePaymentTransactionForm(payment.id, { notes: event.target.value })} />
                          </Field>
                          <button type="submit" className="primary-button" disabled={saving || !transactionForm.amount}>
                            {saving ? "Saving..." : "Record Payment"}
                          </button>
                        </form>
                      )}
                      {payment.transactions.length > 0 && (
                        <div className="payment-transactions span-all">
                          {payment.transactions.map((transaction) => (
                            <div className="payment-transaction" key={transaction.id}>
                              <strong>{formatCurrency(transaction.amount, payment.currency)}</strong>
                              <span>{transaction.method}</span>
                              <span>{formatFollowUpLabel(transaction.paidAt)}</span>
                              <span>{transaction.receiptNumber || "No receipt"}</span>
                              {transaction.referenceNumber && <span>{transaction.referenceNumber}</span>}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </section>

            <section className="drawer-section">
              <div className="section-heading">
                <h3>Admission Application</h3>
                <span>Formal review</span>
              </div>
              <p className="drawer-permission-note">Create an admission application when the lead is ready for document/payment readiness review and enrollment approval.</p>
              <button type="button" className="primary-button" disabled={!canManageLeads || archived || saving} onClick={onCreateApplication}>
                <BookOpen size={16} />
                Create Application
              </button>
            </section>

            <section className="drawer-section">
              <div className="section-heading">
                <h3>Follow-ups</h3>
                <span>{lead.followUps.length}</span>
              </div>
              {rescheduleForm && (
                <form className="inline-editor" onSubmit={handleRescheduleSubmit}>
                  <Field label="Type" error={getFieldError("type")}>
                    <select value={rescheduleForm.type} disabled={!canUseEngagement} onChange={(event) => setRescheduleForm((current) => ({ ...current, type: event.target.value }))}>
                      {["Call", "WhatsApp", "Email", "Walk-in"].map((item) => <option key={item} value={item}>{item}</option>)}
                    </select>
                  </Field>
                  <Field label="Priority" error={getFieldError("priority")}>
                    <select value={rescheduleForm.priority} disabled={!canUseEngagement} onChange={(event) => setRescheduleForm((current) => ({ ...current, priority: event.target.value }))}>
                      {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                    </select>
                  </Field>
                  <Field label="Counsellor" error={getFieldError("assignedUserId")}>
                    <select value={rescheduleForm.assignedUserId} disabled={!canUseEngagement || assigneeLockedToSelf} onChange={(event) => setRescheduleForm((current) => ({ ...current, assignedUserId: event.target.value }))}>
                      <option value="">Current owner</option>
                      {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                    </select>
                  </Field>
                  <Field label="Due At" error={getFieldError("dueAt")} required>
                    <input type="datetime-local" value={rescheduleForm.dueAt} disabled={!canUseEngagement} onChange={(event) => setRescheduleForm((current) => ({ ...current, dueAt: event.target.value }))} required />
                  </Field>
                  <div className="inline-editor-actions">
                    <button type="button" className="ghost-button" onClick={() => setRescheduleForm(null)} disabled={saving}>Cancel</button>
                    <button type="submit" className="primary-button" disabled={!canUseEngagement || !rescheduleForm.dueAt}>{saving ? "Saving..." : "Reschedule Follow-up"}</button>
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
                      {canUseEngagement && item.status === "Scheduled" && (
                        <>
                          <button type="button" className="ghost-button" onClick={() => openRescheduleForm(item)} disabled={saving}>
                            <CalendarDays size={16} />
                            Reschedule
                          </button>
                          <button type="button" className="ghost-button danger-text" onClick={() => onCancelFollowUp(item.id, { version: item.version })} disabled={saving}>
                            <X size={16} />
                            Cancel
                          </button>
                          <button type="button" className="ghost-button" onClick={() => onCompleteFollowUp(item.id, { version: item.version })} disabled={saving}>
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
  const totalLeads = pipeline.reduce((sum, stage) => sum + Number(stage.count || stage.leads?.length || 0), 0);
  const activeStages = pipeline.filter((stage) => Number(stage.count || stage.leads?.length || 0) > 0).length;
  const topStage = pipeline.reduce((current, stage) => {
    const stageCount = Number(stage.count || stage.leads?.length || 0);
    const currentCount = Number(current?.count || current?.leads?.length || 0);
    return stageCount > currentCount ? stage : current;
  }, pipeline[0]);

  return (
    <section className="pipeline-workspace">
      <div className="pipeline-hero">
        <div>
          <span className="eyebrow">Admission pipeline</span>
          <h1>Lead Pipeline</h1>
          <p>Track enquiry movement by stage, prioritize stuck leads, and open student records quickly.</p>
        </div>
        <div className="pipeline-hero-actions">
          <div className="pipeline-hero-stat">
            <span>Total leads</span>
            <strong>{loading ? "..." : formatNumber(totalLeads)}</strong>
          </div>
          <div className="pipeline-hero-stat">
            <span>Active stages</span>
            <strong>{loading ? "..." : formatNumber(activeStages)}</strong>
          </div>
          <button className="primary-button" onClick={() => onNewLead()} disabled={!canManageLeads}>
            <Plus size={18} />
            Create New Lead
          </button>
        </div>
      </div>

      <div className="pipeline-toolbar">
        <div>
          <strong>Board view</strong>
          <span>{topStage ? `${topStage.name} has the highest current volume` : "No stage volume yet"}</span>
        </div>
        <button className="soft-button" onClick={onRetry} disabled={loading}>Refresh</button>
      </div>

      {loading && <StatePanel title="Loading pipeline" message="Fetching live stages from CounselMate API..." />}
      {error && <StatePanel title="Could not load pipeline" message={error} action={onRetry} />}
      {!loading && !error && pipeline.length === 0 && <StatePanel title="No pipeline stages" message="No lead stages are configured for this tenant." />}
      {!loading && !error && pipeline.length > 0 && (
        <div className="kanban">
          {pipeline.map((stage, stageIndex) => {
            const stageLeads = stage.leads || [];
            const stageCount = Number(stage.count || stageLeads.length || 0);
            const stageShare = totalLeads ? Math.round((stageCount / totalLeads) * 100) : 0;
            return (
            <section className="kanban-column" key={stage.name}>
              <header>
                <div>
                  <h3>{stage.name}</h3>
                  <p>{stageShare}% of pipeline</p>
                </div>
                <span>{formatNumber(stageCount)}</span>
              </header>
              <div className="pipeline-stage-track" aria-hidden="true">
                <span style={{ width: `${Math.max(stageShare, stageCount > 0 ? 6 : 0)}%` }} />
              </div>
              {stageLeads.length === 0 && (
                <div className="pipeline-empty-stage">
                  <strong>No leads here</strong>
                  <p>New leads moved to this stage will appear in this column.</p>
                </div>
              )}
              {stageLeads.map((lead) => (
                <article className="lead-card" key={lead.id} onClick={() => onOpenLead(lead.id)} role="button" tabIndex={0} onKeyDown={(event) => event.key === "Enter" && onOpenLead(lead.id)}>
                  <div className="lead-card-top">
                    <Badge label={(lead.course || "Course").split(" ")[0]} />
                    <span className="lead-card-index">#{stageIndex + 1}</span>
                  </div>
                  <h4>{lead.studentName}</h4>
                  <p>{lead.course}</p>
                  <footer>
                    <span className={["High", "Urgent"].includes(lead.priority) ? "danger-text" : ""}>{formatFollowUpLabel(lead.nextFollowUpAt)}</span>
                    <span className="mini-avatar">{initials(lead.studentName)}</span>
                  </footer>
                </article>
              ))}
              <button className="add-card" type="button" onClick={() => onNewLead(stage.name)} disabled={!canManageLeads}>
                <Plus size={18} />
                Add lead to {stage.name}
              </button>
            </section>
          );
          })}
        </div>
      )}
    </section>
  );
}

function FollowUpsPage({
  followUps,
  loading,
  error,
  actionStatus,
  onRetry,
  onOpenLead,
  onComplete,
  onCancel,
  onReschedule,
  canManageLeads,
}) {
  const [activeTab, setActiveTab] = useState("today");
  const [query, setQuery] = useState("");
  const [priorityFilter, setPriorityFilter] = useState("all");
  const [rescheduling, setRescheduling] = useState(null);

  const counts = useMemo(() => getFollowUpQueueCounts(followUps), [followUps]);
  const filteredFollowUps = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    return followUps
      .filter((item) => followUpMatchesQueue(item, activeTab))
      .filter((item) => priorityFilter === "all" || item.priority === priorityFilter)
      .filter((item) => {
        if (!normalizedQuery) {
          return true;
        }
        return `${item.studentName} ${item.leadId} ${item.assignedTo} ${item.type}`.toLocaleLowerCase().includes(normalizedQuery);
      })
      .sort(compareFollowUpsForQueue);
  }, [activeTab, followUps, priorityFilter, query]);

  const scheduledCount = followUps.filter((item) => item.status === "Scheduled").length;
  const completedToday = followUps.filter((item) => item.status === "Completed" && isToday(item.completedAt || item.updatedAt)).length;
  const rescheduleFieldErrors = rescheduling ? actionStatus.fieldErrors || {} : {};

  const startReschedule = (item) => {
    setRescheduling({
      id: item.id,
      leadId: item.leadId,
      type: item.type || "Call",
      priority: item.priority || "Medium",
      assignedUserId: "",
      dueAt: toDateTimeLocalValue(item.dueAt) || defaultFutureDateTimeLocal(),
      version: item.version,
    });
  };

  const submitReschedule = async (event) => {
    event.preventDefault();
    if (!rescheduling?.dueAt) {
      return;
    }
    const dueDate = new Date(rescheduling.dueAt);
    if (Number.isNaN(dueDate.getTime())) {
      return;
    }

    const updated = await onReschedule(rescheduling, {
      type: rescheduling.type,
      priority: rescheduling.priority,
      assignedUserId: optionalValue(rescheduling.assignedUserId),
      dueAt: dueDate.toISOString(),
      version: rescheduling.version,
    });
    if (updated) {
      setRescheduling(null);
    }
  };

  const clearFilters = () => {
    setQuery("");
    setPriorityFilter("all");
  };

  return (
    <section className="followup-page">
      <div className="followup-hero">
        <div>
          <span className="eyebrow">Counsellor queue</span>
          <h1>Follow-ups</h1>
          <p>Run today’s admission calls, recover overdue tasks, and keep every student conversation moving.</p>
        </div>
        <div className="followup-hero-actions">
          <div className="followup-hero-stat">
            <span>Open queue</span>
            <strong>{loading ? "..." : formatNumber(scheduledCount)}</strong>
          </div>
          <div className="followup-hero-stat danger">
            <span>Overdue</span>
            <strong>{loading ? "..." : formatNumber(counts.overdue)}</strong>
          </div>
          <button className="soft-button" type="button" onClick={onRetry} disabled={loading}>Refresh</button>
        </div>
      </div>

      <div className="followup-workspace">
        <section>
          <div className="followup-summary">
            <div>
              <span>Scheduled</span>
              <strong>{formatNumber(scheduledCount)}</strong>
            </div>
            <div>
              <span>Overdue</span>
              <strong>{formatNumber(counts.overdue)}</strong>
            </div>
            <div>
              <span>Due Today</span>
              <strong>{formatNumber(counts.today)}</strong>
            </div>
            <div>
              <span>Completed Today</span>
              <strong>{formatNumber(completedToday)}</strong>
            </div>
          </div>

          {actionStatus.error && <div className="form-alert">{actionStatus.error}</div>}

          <div className="followup-toolbar">
            <label className="lead-search-field">
              <span>Search</span>
              <div className="lead-search-input">
                <Search size={16} />
                <input value={query} type="search" placeholder="Student, lead ID, owner, type" onChange={(event) => setQuery(event.target.value)} />
              </div>
            </label>
            <label>
              <span>Priority</span>
              <select value={priorityFilter} onChange={(event) => setPriorityFilter(event.target.value)}>
                <option value="all">All priorities</option>
                {["Urgent", "High", "Medium", "Low"].map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
            </label>
            <button className="soft-button" type="button" onClick={clearFilters} disabled={!query && priorityFilter === "all"}>Clear</button>
          </div>

          <div className="tabs followup-tabs">
            {[
              ["overdue", "Overdue", counts.overdue],
              ["today", "Today", counts.today],
              ["upcoming", "Upcoming", counts.upcoming],
              ["completed", "Completed", counts.completed],
              ["cancelled", "Cancelled", counts.cancelled],
              ["all", "All", followUps.length],
            ].map(([id, label, count]) => (
              <button key={id} type="button" className={activeTab === id ? "active" : ""} onClick={() => setActiveTab(id)}>
                {label} <span>{formatNumber(count)}</span>
              </button>
            ))}
          </div>

          <div className="followup-list">
            {loading && <StatePanel title="Loading follow-ups" message="Fetching live scheduled tasks..." />}
            {error && <StatePanel title="Could not load follow-ups" message={error} action={onRetry} />}
            {!loading && !error && followUps.length === 0 && <StatePanel title="No follow-ups" message="No follow-ups were found for your current access scope." />}
            {!loading && !error && followUps.length > 0 && filteredFollowUps.length === 0 && (
              <StatePanel title="No matching follow-ups" message="Change the queue tab or clear the current filters." action={clearFilters} actionLabel="Clear filters" />
            )}
            {!loading && !error && filteredFollowUps.map((item) => (
              <FollowUpRow
                key={item.id}
                item={item}
                saving={actionStatus.savingId === item.id}
                canManageLeads={canManageLeads}
                onOpenLead={onOpenLead}
                onComplete={onComplete}
                onCancel={onCancel}
                onReschedule={startReschedule}
              />
            ))}
          </div>

          <p className="drawer-permission-note followup-note">Create new follow-ups from a lead detail drawer so every task stays attached to a lead.</p>
        </section>

        <aside className="right-rail">
          <Card title="Queue Rules">
            <div className="followup-rules">
              <p><strong>Overdue:</strong> scheduled follow-ups before now.</p>
              <p><strong>Today:</strong> scheduled follow-ups due before midnight.</p>
              <p><strong>Upcoming:</strong> scheduled follow-ups after today.</p>
              <p><strong>Completed/cancelled:</strong> read-only history.</p>
            </div>
          </Card>
          <div className="mini-metrics">
            <Metric title="Queue" value={formatNumber(filteredFollowUps.length)} />
            <Metric title="All Tasks" value={formatNumber(followUps.length)} />
          </div>
        </aside>
      </div>

      {rescheduling && (
        <div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !actionStatus.savingId && setRescheduling(null)}>
          <form className="team-modal password-modal" onSubmit={submitReschedule} role="dialog" aria-modal="true" aria-labelledby="followup-reschedule-title">
            <header className="modal-header">
              <div>
                <h2 id="followup-reschedule-title">Reschedule Follow-up</h2>
                <p>{rescheduling.leadId}</p>
              </div>
              <button type="button" className="icon-button modal-close" onClick={() => setRescheduling(null)} disabled={Boolean(actionStatus.savingId)} aria-label="Close">
                <X size={20} />
              </button>
            </header>
            <div className="team-modal-body">
              <div className="form-grid compact">
                <Field label="Type" error={firstError(rescheduleFieldErrors.type)}>
                  <select value={rescheduling.type} onChange={(event) => setRescheduling((current) => ({ ...current, type: event.target.value }))}>
                    {["Call", "WhatsApp", "Email", "Walk-in"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Priority" error={firstError(rescheduleFieldErrors.priority)}>
                  <select value={rescheduling.priority} onChange={(event) => setRescheduling((current) => ({ ...current, priority: event.target.value }))}>
                    {["Low", "Medium", "High", "Urgent"].map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </Field>
                <Field label="Due At" error={firstError(rescheduleFieldErrors.dueAt)} className="span-2" required>
                  <input type="datetime-local" value={rescheduling.dueAt} onChange={(event) => setRescheduling((current) => ({ ...current, dueAt: event.target.value }))} required />
                </Field>
              </div>
            </div>
            <footer className="team-modal-actions">
              <button type="button" className="ghost-button" onClick={() => setRescheduling(null)} disabled={Boolean(actionStatus.savingId)}>Cancel</button>
              <button type="submit" className="primary-button" disabled={Boolean(actionStatus.savingId) || !rescheduling.dueAt}>
                {actionStatus.savingId ? "Saving..." : "Reschedule Follow-up"}
              </button>
            </footer>
          </form>
        </div>
      )}
    </section>
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

function ReportsPage({
  reports,
  options,
  filters,
  status,
  canExportReports,
  onFiltersChange,
  onResetFilters,
  onExport,
  onRetry,
  currentUser,
  onOpenLead,
}) {
  if (["Counselor", "Telecaller"].includes(currentUser?.role)) {
    return (
      <CounsellorWorkInsights
        reports={reports}
        options={options}
        filters={filters}
        status={status}
        onFiltersChange={onFiltersChange}
        onResetFilters={onResetFilters}
        onRetry={onRetry}
        onOpenLead={onOpenLead}
      />
    );
  }

  const summary = reports?.summary || {};
  const hasFilters = JSON.stringify(filters) !== JSON.stringify(defaultReportFilters());

  return (
    <>
      <PageTitle
        title="Admissions Reports"
        subtitle={reports ? `${reports.access.scope} | ${reports.startDate} to ${reports.endDate}` : "Performance metrics and conversion insights."}
        action={
          canExportReports && (
            <div className="page-actions">
              <button className="secondary-button" onClick={() => onExport("csv")} disabled={Boolean(status.exporting)}>
                <Download size={18} />
                {status.exporting === "csv" ? "Exporting..." : "CSV"}
              </button>
              <button className="primary-button" onClick={() => onExport("xlsx")} disabled={Boolean(status.exporting)}>
                <Download size={18} />
                {status.exporting === "xlsx" ? "Exporting..." : "XLSX"}
              </button>
            </div>
          )
        }
      />

      <div className="report-filter-panel">
        <Field label="Start Date">
          <input type="date" value={filters.startDate} onChange={(event) => onFiltersChange({ startDate: event.target.value })} />
        </Field>
        <Field label="End Date">
          <input type="date" value={filters.endDate} onChange={(event) => onFiltersChange({ endDate: event.target.value })} />
        </Field>
        <Field label="Branch">
          <select value={filters.branchId} onChange={(event) => onFiltersChange({ branchId: event.target.value })}>
            <option value="">All branches</option>
            {options.branches.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <Field label="Course">
          <select value={filters.courseId} onChange={(event) => onFiltersChange({ courseId: event.target.value })}>
            <option value="">All courses</option>
            {options.courses.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <Field label="Source">
          <select value={filters.sourceId} onChange={(event) => onFiltersChange({ sourceId: event.target.value })}>
            <option value="">All sources</option>
            {options.sources.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <Field label="Counsellor">
          <select value={filters.assignedUserId} onChange={(event) => onFiltersChange({ assignedUserId: event.target.value })}>
            <option value="">All counsellors</option>
            {options.counselors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <div className="lead-filter-actions">
          <strong>{status.loading ? "Loading..." : reports ? `Generated ${formatFollowUpLabel(reports.generatedAt)}` : "No report loaded"}</strong>
          <button className="soft-button" type="button" onClick={onResetFilters} disabled={!hasFilters || status.loading}>Reset</button>
        </div>
      </div>

      {status.error && <div className="form-alert">{status.error}</div>}

      {status.loading && !reports && <StatePanel title="Loading reports" message="Calculating tenant metrics..." />}
      {!status.loading && !status.error && !reports && <StatePanel title="No reports loaded" message="Use the filters to load admissions reports." action={onRetry} actionLabel="Load reports" />}

      {reports && (
        <div className="reports-grid">
          <div className="metric-stack">
            <Metric title="Total Leads" value={formatNumber(summary.totalLeads)} />
            <Metric title="Conversion Rate" value={`${summary.conversionRate || 0}%`} />
            <Metric title="Open Leads" value={formatNumber(summary.openLeads)} />
            <Metric title="Overdue Follow-ups" value={formatNumber(summary.overdueFollowUps)} warning={summary.overdueFollowUps > 0} />
          </div>

          <Card title="Pipeline Funnel" className="funnel-card">
            <div className="funnel">
              {reports.stages.length === 0 && <StatePanel title="No stage data" message="No leads were created in this period." />}
              {reports.stages.map((stage) => (
                <div key={stage.stageId} style={{ width: `${Math.max(18, stage.percentage || 0)}%` }}>
                  <strong>{stage.stage}</strong>
                  <span>{formatNumber(stage.totalLeads)} | {stage.percentage}%</span>
                </div>
              ))}
            </div>
          </Card>

          <ReportTable
            title="Lead Source Performance"
            emptyTitle="No source data"
            emptyMessage="No leads match the current source filters."
            columns={["Source", "Leads", "Won", "Lost", "Open", "Conversion"]}
            rows={reports.sources}
            renderRow={(item) => (
              <tr key={item.sourceId}>
                <td><strong>{item.source}</strong></td>
                <td>{formatNumber(item.totalLeads)}</td>
                <td>{formatNumber(item.wonLeads)}</td>
                <td>{formatNumber(item.lostLeads)}</td>
                <td>{formatNumber(item.openLeads)}</td>
                <td>{item.conversionRate}%</td>
              </tr>
            )}
          />

          <ReportTable
            title="Counsellor Performance"
            emptyTitle="No counsellor data"
            emptyMessage="No assigned lead or follow-up activity matches this period."
            columns={["Counsellor", "Leads", "Won", "Scheduled", "Completed", "Overdue", "Conversion"]}
            rows={reports.counselors}
            renderRow={(item) => (
              <tr key={item.userId || item.counselor}>
                <td><strong>{item.counselor}</strong></td>
                <td>{formatNumber(item.totalLeads)}</td>
                <td>{formatNumber(item.wonLeads)}</td>
                <td>{formatNumber(item.scheduledFollowUps)}</td>
                <td>{formatNumber(item.completedFollowUps)}</td>
                <td>{formatNumber(item.overdueFollowUps)}</td>
                <td>{item.conversionRate}%</td>
              </tr>
            )}
          />

          <ReportTable
            title="Stage Distribution"
            emptyTitle="No stage data"
            emptyMessage="No pipeline stage activity matches this period."
            columns={["Stage", "Leads", "Share", "Type"]}
            rows={reports.stages}
            renderRow={(item) => (
              <tr key={item.stageId}>
                <td><strong>{item.stage}</strong></td>
                <td>{formatNumber(item.totalLeads)}</td>
                <td>{item.percentage}%</td>
                <td>{item.isWonStage ? "Won" : item.isLostStage ? "Lost" : "Open"}</td>
              </tr>
            )}
          />
        </div>
      )}
    </>
  );
}

function EnrollmentsPage({
  enrollments,
  filters,
  options,
  status,
  selectedEnrollment,
  currentUser,
  onFiltersChange,
  onRetry,
  onOpen,
  onCloseDetail,
  onStatusUpdate,
}) {
  const items = enrollments?.items || [];
  const totalPages = Math.max(1, Math.ceil((enrollments?.total || 0) / Math.max(enrollments?.pageSize || 25, 1)));
  return (
    <>
      <PageTitle title="Enrolled Students" subtitle="Manage admitted student records, enrollment status, and admission readiness snapshots." />
      <div className="report-filter-panel enrollment-filter-panel">
        <Field label="Search"><input value={filters.search} placeholder="Student, ENR, APP, or lead" onChange={(event) => onFiltersChange({ search: event.target.value })} /></Field>
        <Field label="Status">
          <select value={filters.status} onChange={(event) => onFiltersChange({ status: event.target.value })}>
            <option value="">All statuses</option>
            {["Active", "Deferred", "Completed", "Cancelled"].map((item) => <option key={item} value={item}>{item}</option>)}
          </select>
        </Field>
        <Field label="Course">
          <select value={filters.courseId} onChange={(event) => onFiltersChange({ courseId: event.target.value })}>
            <option value="">All courses</option>
            {(options.courses || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <Field label="Branch">
          <select value={filters.branchId} onChange={(event) => onFiltersChange({ branchId: event.target.value })}>
            <option value="">All branches</option>
            {(options.branches || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <Field label="Intake"><input value={filters.intake} maxLength={120} placeholder="e.g. July 2026" onChange={(event) => onFiltersChange({ intake: event.target.value })} /></Field>
        <div className="lead-filter-actions">
          <strong>{status.loading ? "Loading..." : `${enrollments?.total || 0} enrolled students`}</strong>
          <button className="soft-button" type="button" onClick={onRetry} disabled={status.loading}>Refresh</button>
        </div>
      </div>
      {status.error && <div className="form-alert" role="alert">{status.error}</div>}
      {status.message && <div className="form-success" role="status">{status.message}</div>}
      <div className="table-card">
        {status.loading && <StatePanel title="Loading enrollments" message="Fetching admitted student records..." />}
        {!status.loading && items.length === 0 && <StatePanel title="No enrollments" message="Approved applications will appear here after enrollment is completed." />}
        {!status.loading && items.length > 0 && (
          <table>
            <thead><tr><th>Enrollment</th><th>Student</th><th>Course</th><th>Branch</th><th>Status</th><th>Fee Balance</th><th>Documents</th><th>Enrolled</th></tr></thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="clickable-row" onClick={() => onOpen(item.id)}>
                  <td><strong>{item.id}</strong><br /><small>{item.applicationId}</small></td>
                  <td>{item.studentName}<br /><small>{item.leadId}</small></td>
                  <td>{item.course}{item.intake ? <small> · {item.intake}</small> : null}</td>
                  <td>{item.branch || "No branch"}</td>
                  <td><Status status={item.status} /></td>
                  <td>{formatCurrency(item.feeBalance, "INR")}</td>
                  <td><Badge label={item.documentsReady ? "Ready" : "Pending"} muted={!item.documentsReady} /></td>
                  <td>{formatDate(item.enrolledAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <footer className="table-footer">
          <span>Page {enrollments?.page || 1} of {totalPages}</span>
          <div>
            <button className="pager" disabled={(enrollments?.page || 1) <= 1 || status.loading} onClick={() => onFiltersChange({ page: (enrollments?.page || 1) - 1 })}>Prev</button>
            <button className="pager active" disabled>{enrollments?.page || 1}</button>
            <button className="pager" disabled={(enrollments?.page || 1) >= totalPages || status.loading} onClick={() => onFiltersChange({ page: (enrollments?.page || 1) + 1 })}>Next</button>
          </div>
        </footer>
      </div>
      {selectedEnrollment && <EnrollmentDetailPanel enrollment={selectedEnrollment} currentUser={currentUser} saving={status.saving} onClose={onCloseDetail} onStatusUpdate={onStatusUpdate} />}
    </>
  );
}

function EnrollmentDetailPanel({ enrollment, currentUser, saving, onClose, onStatusUpdate }) {
  const [note, setNote] = useState("");
  const canManage = ["Owner", "Admin", "BranchManager"].includes(currentUser?.role);
  const isOwnerOrAdmin = ["Owner", "Admin"].includes(currentUser?.role);
  const availableStatuses = enrollment.status === "Active"
    ? ["Deferred", "Cancelled", "Completed"]
    : enrollment.status === "Deferred"
      ? ["Active", "Cancelled"]
      : isOwnerOrAdmin ? ["Active"] : [];

  useEffect(() => setNote(""), [enrollment.id]);

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !saving && onClose()}>
      <aside className="lead-drawer application-drawer" role="dialog" aria-modal="true" aria-labelledby="enrollment-detail-title">
        <header className="drawer-header">
          <div><span className="eyebrow">{enrollment.id}</span><h2 id="enrollment-detail-title">{enrollment.studentName}</h2></div>
          <button className="icon-button" onClick={onClose} disabled={saving} aria-label="Close enrollment"><X size={20} /></button>
        </header>
        <div className="drawer-body">
          <section className="lead-summary">
            <div className="lead-avatar">{initials(enrollment.studentName)}</div>
            <div><h3>{enrollment.course}</h3><p>{enrollment.applicationId} · {enrollment.leadId}</p></div>
            <Status status={enrollment.status} />
          </section>
          {enrollment.archivedAt && <div className="form-alert">The linked lead is archived. Enrollment history remains available.</div>}
          <section className="drawer-section">
            <div className="section-heading"><h3>Student Profile</h3></div>
            <div className="detail-grid">
              <InfoItem label="Phone" value={enrollment.phone || "Not added"} />
              <InfoItem label="Email" value={enrollment.email || "Not added"} />
              <InfoItem label="Guardian" value={enrollment.guardianName || "Not added"} />
              <InfoItem label="City" value={enrollment.city || "Not added"} />
              <InfoItem label="Branch" value={enrollment.branch || "No branch"} />
              <InfoItem label="Intake" value={enrollment.intake || "Not added"} />
              <InfoItem label="Enrolled" value={formatDate(enrollment.enrolledAt)} />
              <InfoItem label="Updated" value={formatFollowUpLabel(enrollment.updatedAt)} />
            </div>
          </section>
          <section className="drawer-section">
            <div className="section-heading"><h3>Admission Snapshot</h3></div>
            <div className="admission-readiness">
              <MetricPill label="Application" value={formatApplicationStatus(enrollment.applicationStatus)} />
              <MetricPill label="Checklist" value={`${enrollment.checklistDone}/${enrollment.checklistTotal}`} tone={enrollment.checklistDone < enrollment.checklistTotal ? "warn" : ""} />
              <MetricPill label="Documents" value={`${enrollment.verifiedRequiredDocuments}/${enrollment.requiredDocuments}`} tone={enrollment.verifiedRequiredDocuments < enrollment.requiredDocuments ? "warn" : ""} />
            </div>
            <div className="fee-summary-grid">
              <InfoItem label="Total Due" value={formatCurrency(enrollment.totalDue, enrollment.currency)} />
              <InfoItem label="Total Paid" value={formatCurrency(enrollment.totalPaid, enrollment.currency)} />
              <InfoItem label="Balance" value={formatCurrency(enrollment.balance, enrollment.currency)} />
              <InfoItem label="Created By" value={enrollment.createdBy} />
            </div>
          </section>
          <section className="drawer-section">
            <div className="section-heading"><h3>Status Actions</h3></div>
            {!canManage && <p className="drawer-permission-note">Only owner, admin, and branch manager roles can change enrollment status.</p>}
            {canManage && availableStatuses.length === 0 && <p className="drawer-permission-note">This is a final status. Only owners and admins can reopen it.</p>}
            {canManage && availableStatuses.length > 0 && (
              <>
                <Field label="Status Note"><textarea value={note} maxLength={500} disabled={saving} placeholder="Optional reason for this status change" onChange={(event) => setNote(event.target.value)} /></Field>
                <div className="application-actions">
                  {availableStatuses.map((nextStatus) => <button key={nextStatus} className={nextStatus === "Cancelled" ? "ghost-button danger-text" : "ghost-button"} disabled={saving} onClick={() => onStatusUpdate(enrollment, nextStatus, note)}>{nextStatus}</button>)}
                </div>
              </>
            )}
          </section>
          <section className="drawer-section">
            <div className="section-heading"><h3>Recent Activity</h3><span>{enrollment.recentActivities.length}</span></div>
            <div className="timeline">
              {enrollment.recentActivities.length === 0 && <p className="muted-text">No recent activity.</p>}
              {enrollment.recentActivities.map((item) => (
                <div className="timeline-item" key={item.id}><span className="timeline-dot note" /><div><strong>{item.type}</strong><p>{item.description}</p><small>{formatFollowUpLabel(item.createdAt)} · {item.createdBy}</small></div></div>
              ))}
            </div>
          </section>
        </div>
      </aside>
    </div>
  );
}

function ApplicationsPage({
  applications,
  filters,
  status,
  selectedApplication,
  onFiltersChange,
  onRetry,
  onOpen,
  onCloseDetail,
  onTransition,
  onChecklist,
  onEnroll,
  onUploadDocument,
  onVerifyDocument,
  onRejectDocument,
  onDeleteDocument,
  onDownloadDocument,
  onCreatePayment,
  onUpdatePayment,
  onAddPaymentTransaction,
  onCancelPayment,
  currentUser,
  canManageLeads,
  canManagePayments,
}) {
  const items = applications?.items || [];
  const totalPages = Math.max(1, Math.ceil((applications?.total || 0) / Math.max(applications?.pageSize || 25, 1)));
  return (
    <>
      <PageTitle
        title="Applications & Enrollment"
        subtitle="Review admission applications, checklist readiness, and final enrollment."
      />
      <div className="report-filter-panel application-filter-panel">
        <Field label="Search">
          <input value={filters.search} placeholder="Student or APP number" onChange={(event) => onFiltersChange({ search: event.target.value })} />
        </Field>
        <Field label="Status">
          <select value={filters.status} onChange={(event) => onFiltersChange({ status: event.target.value })}>
            <option value="">All statuses</option>
            {["Draft", "Submitted", "UnderReview", "ChangesRequired", "Approved", "Enrolled", "Rejected", "Withdrawn", "Cancelled"].map((item) => <option key={item} value={item}>{formatApplicationStatus(item)}</option>)}
          </select>
        </Field>
        <div className="lead-filter-actions">
          <strong>{status.loading ? "Loading..." : `${applications?.total || 0} applications`}</strong>
          <button className="soft-button" onClick={onRetry} disabled={status.loading}>Refresh</button>
        </div>
      </div>
      {status.error && <div className="form-alert" role="alert">{status.error}</div>}
      {status.message && <div className="form-success" role="status">{status.message}</div>}
      <div className="table-card">
        {status.loading && <StatePanel title="Loading applications" message="Fetching admission applications..." />}
        {!status.loading && items.length === 0 && <StatePanel title="No applications" message="Create an application from a lead drawer when a lead is ready for admission review." />}
        {!status.loading && items.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Application</th>
                <th>Student</th>
                <th>Course</th>
                <th>Status</th>
                <th>Checklist</th>
                <th>Updated</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="clickable-row" onClick={() => onOpen(item.id)}>
                  <td><strong>{item.id}</strong><br /><small>{item.leadId}</small></td>
                  <td>{item.studentName}</td>
                  <td>{item.course}{item.intake ? <small> · {item.intake}</small> : null}</td>
                  <td><Status status={formatApplicationStatus(item.status)} /></td>
                  <td>{item.checklistDone}/{item.checklistTotal}</td>
                  <td>{formatFollowUpLabel(item.updatedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <footer className="table-footer">
          <span>Page {applications?.page || 1} of {totalPages}</span>
          <div>
            <button className="pager" disabled={(applications?.page || 1) <= 1 || status.loading} onClick={() => onFiltersChange({ page: (applications?.page || 1) - 1 })}>Prev</button>
            <button className="pager active" disabled>{applications?.page || 1}</button>
            <button className="pager" disabled={(applications?.page || 1) >= totalPages || status.loading} onClick={() => onFiltersChange({ page: (applications?.page || 1) + 1 })}>Next</button>
          </div>
        </footer>
      </div>
      {selectedApplication && (
        <ApplicationDetailPanel
          application={selectedApplication}
          saving={status.saving}
          onClose={onCloseDetail}
          onTransition={onTransition}
          onChecklist={onChecklist}
          onEnroll={onEnroll}
          onUploadDocument={onUploadDocument}
          onVerifyDocument={onVerifyDocument}
          onRejectDocument={onRejectDocument}
          onDeleteDocument={onDeleteDocument}
          onDownloadDocument={onDownloadDocument}
          onCreatePayment={onCreatePayment}
          onUpdatePayment={onUpdatePayment}
          onAddPaymentTransaction={onAddPaymentTransaction}
          onCancelPayment={onCancelPayment}
          currentUser={currentUser}
          canManageLeads={canManageLeads}
          canManagePayments={canManagePayments}
        />
      )}
    </>
  );
}

function ApplicationDetailPanel({
  application,
  saving,
  onClose,
  onTransition,
  onChecklist,
  onEnroll,
  onUploadDocument,
  onVerifyDocument,
  onRejectDocument,
  onDeleteDocument,
  onDownloadDocument,
  onCreatePayment,
  onUpdatePayment,
  onAddPaymentTransaction,
  onCancelPayment,
  currentUser,
  canManageLeads,
  canManagePayments,
}) {
  const readiness = application.readiness || {};
  const [documentForms, setDocumentForms] = useState({});
  const [paymentForm, setPaymentForm] = useState(createDefaultPaymentForm());
  const [paymentTransactionForms, setPaymentTransactionForms] = useState({});
  const [editingPaymentId, setEditingPaymentId] = useState("");
  const [editingPaymentForm, setEditingPaymentForm] = useState(null);
  const documents = application.documents || [];
  const payments = application.payments || [];
  const paymentSummary = application.paymentSummary || {
    totalDue: payments.filter((item) => item.status !== "Cancelled").reduce((sum, item) => sum + Number(item.amountDue || 0), 0),
    totalPaid: payments.filter((item) => item.status !== "Cancelled").reduce((sum, item) => sum + Number(item.amountPaid || 0), 0),
    balance: payments.filter((item) => item.status !== "Cancelled").reduce((sum, item) => sum + Number(item.balance || 0), 0),
    currency: "INR",
    paymentsReady: true,
    receiptDocumentVerified: false,
  };
  const archived = Boolean(application.archivedAt);
  const canUploadDocuments = canManageLeads && !archived && !saving && !["Enrolled", "Cancelled"].includes(application.status);
  const canReviewDocuments = ["Owner", "Admin", "BranchManager"].includes(currentUser?.role) && !archived && !saving && application.status !== "Enrolled";
  const canManageApplicationPayments = canManagePayments && !archived && !saving && !["Enrolled", "Cancelled"].includes(application.status);
  const missingRequiredDocuments = documents.filter((item) => item.isRequired && !item.documentId).length;
  const pendingRequiredDocuments = documents.filter((item) => item.isRequired && item.documentId && item.status !== "Verified").length;

  useEffect(() => {
    setDocumentForms({});
    setPaymentForm(createDefaultPaymentForm());
    setPaymentTransactionForms({});
    setEditingPaymentId("");
    setEditingPaymentForm(null);
  }, [application.id]);

  const updateDocumentForm = (documentTypeId, updates) => {
    setDocumentForms((current) => ({
      ...current,
      [documentTypeId]: {
        file: null,
        notes: "",
        rejectNotes: "",
        ...(current[documentTypeId] || {}),
        ...updates,
      },
    }));
  };

  const handleDocumentUpload = async (event, document) => {
    event.preventDefault();
    const form = documentForms[document.documentTypeId] || {};
    if (!canUploadDocuments || !form.file) {
      return;
    }

    const updated = await onUploadDocument(application, {
      documentTypeId: document.documentTypeId,
      file: form.file,
      version: document.version,
      notes: optionalValue(form.notes),
    });
    if (updated) {
      updateDocumentForm(document.documentTypeId, { file: null, notes: "" });
    }
  };

  const handleDocumentReject = async (document) => {
    const notes = documentForms[document.documentTypeId]?.rejectNotes || "";
    if (!canReviewDocuments || !document.documentId || !notes.trim()) {
      return;
    }

    const updated = await onRejectDocument(application, document, notes.trim());
    if (updated) {
      updateDocumentForm(document.documentTypeId, { rejectNotes: "" });
    }
  };

  const openPaymentEdit = (payment) => {
    setEditingPaymentId(payment.id);
    setEditingPaymentForm({
      title: payment.title,
      amountDue: String(payment.amountDue),
      dueDate: payment.dueDate ? toDateInputValue(new Date(payment.dueDate)) : "",
      notes: payment.notes || "",
    });
  };

  const handlePaymentCreate = async (event) => {
    event.preventDefault();
    if (!canManageApplicationPayments || !paymentForm.title.trim() || !paymentForm.amountDue || Number(paymentForm.amountDue) <= 0) {
      return;
    }

    const updated = await onCreatePayment(application, {
      title: paymentForm.title.trim(),
      amountDue: Number(paymentForm.amountDue),
      currency: "INR",
      dueDate: paymentForm.dueDate ? new Date(`${paymentForm.dueDate}T00:00:00`).toISOString() : null,
      notes: optionalValue(paymentForm.notes),
      version: 0,
    });
    if (updated) {
      setPaymentForm(createDefaultPaymentForm());
    }
  };

  const handlePaymentUpdate = async (event) => {
    event.preventDefault();
    const payment = payments.find((item) => item.id === editingPaymentId);
    if (!canManageApplicationPayments || !payment || !editingPaymentForm?.title.trim() || !editingPaymentForm.amountDue || Number(editingPaymentForm.amountDue) < Number(payment.amountPaid || 0)) {
      return;
    }

    const updated = await onUpdatePayment(application, payment, {
      title: editingPaymentForm.title.trim(),
      amountDue: Number(editingPaymentForm.amountDue),
      currency: "INR",
      dueDate: editingPaymentForm.dueDate ? new Date(`${editingPaymentForm.dueDate}T00:00:00`).toISOString() : null,
      notes: optionalValue(editingPaymentForm.notes),
      version: payment.version,
    });
    if (updated) {
      setEditingPaymentId("");
      setEditingPaymentForm(null);
    }
  };

  const updatePaymentTransactionForm = (paymentId, updates) => {
    setPaymentTransactionForms((current) => ({
      ...current,
      [paymentId]: {
        amount: "",
        method: "UPI",
        referenceNumber: "",
        receiptNumber: "",
        paidAt: "",
        notes: "",
        ...(current[paymentId] || {}),
        ...updates,
      },
    }));
  };

  const handlePaymentTransactionCreate = async (event, payment) => {
    event.preventDefault();
    const form = paymentTransactionForms[payment.id] || {};
    const amount = Number(form.amount);
    if (!canManageApplicationPayments || !amount || amount <= 0 || amount > Number(payment.balance || 0)) {
      return;
    }

    const updated = await onAddPaymentTransaction(application, payment, {
      amount,
      method: form.method || "UPI",
      referenceNumber: optionalValue(form.referenceNumber),
      receiptNumber: optionalValue(form.receiptNumber),
      paidAt: form.paidAt ? new Date(form.paidAt).toISOString() : null,
      notes: optionalValue(form.notes),
      version: payment.version,
    });
    if (updated) {
      updatePaymentTransactionForm(payment.id, {
        amount: "",
        referenceNumber: "",
        receiptNumber: "",
        paidAt: "",
        notes: "",
      });
    }
  };

  return (
    <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && !saving && onClose()}>
      <aside className="lead-drawer application-drawer" role="dialog" aria-modal="true" aria-labelledby="application-detail-title">
        <header className="drawer-header">
          <div>
            <span className="eyebrow">{application.id}</span>
            <h2 id="application-detail-title">{application.studentName}</h2>
          </div>
          <button className="icon-button" onClick={onClose} disabled={saving} aria-label="Close application"><X size={20} /></button>
        </header>
        <div className="drawer-body">
          <section className="lead-summary">
            <div className="lead-avatar">{initials(application.studentName)}</div>
            <div>
              <h3>{application.course}</h3>
              <p>{application.leadId}{application.intake ? ` · ${application.intake}` : ""}</p>
            </div>
            <Status status={formatApplicationStatus(application.status)} />
          </section>
          <div className="admission-readiness">
            <MetricPill label="Checklist Missing" value={readiness.requiredChecklistMissing ?? 0} tone={(readiness.requiredChecklistMissing || 0) > 0 ? "warn" : ""} />
            <MetricPill label="Documents" value={`${readiness.verifiedRequiredDocuments || 0}/${readiness.requiredDocuments || 0}`} tone={!readiness.documentsReady ? "warn" : ""} />
            <MetricPill label="Fee Balance" value={formatCurrency(readiness.unpaidBalance || 0, "INR")} tone={!readiness.paymentsReady ? "warn" : ""} />
          </div>
          {(!readiness.documentsReady || missingRequiredDocuments > 0 || pendingRequiredDocuments > 0) && (
            <div className="application-blocker-note">
              <strong>Document readiness blocked</strong>
              <span>
                {missingRequiredDocuments > 0 ? `${missingRequiredDocuments} required missing` : "No required documents missing"}
                {pendingRequiredDocuments > 0 ? ` · ${pendingRequiredDocuments} required pending/rejected` : ""}
              </span>
            </div>
          )}
          <section className="drawer-section">
            <div className="section-heading"><h3>Actions</h3></div>
            <div className="application-actions">
              {["Submitted", "UnderReview", "ChangesRequired", "Approved", "Rejected", "Withdrawn", "Cancelled"].map((status) => (
                <button key={status} className="ghost-button" disabled={saving || application.status === status} onClick={() => onTransition(application, status)}>
                  {formatApplicationStatus(status)}
                </button>
              ))}
              <button className="primary-button" disabled={saving || application.status !== "Approved"} onClick={() => onEnroll(application)}>
                Complete Enrollment
              </button>
            </div>
          </section>
          <section className="drawer-section">
            <div className="section-heading"><h3>Admission Checklist</h3><span>{application.checklist.filter((item) => item.isCompleted || item.isWaived).length}/{application.checklist.length}</span></div>
            <div className="document-list">
              {application.checklist.map((item) => (
                <div className="document-row application-check-row" key={item.id}>
                  <div className="document-main">
                    <div className="document-title-row"><strong>{item.name}</strong><Badge label={item.isWaived ? "Waived" : item.isCompleted ? "Complete" : "Pending"} muted={!item.isCompleted && !item.isWaived} /></div>
                    <small>{item.category}{item.isRequired ? " · Required" : " · Optional"}</small>
                    {item.notes && <p>{item.notes}</p>}
                  </div>
                  <div className="document-actions">
                    <button className="ghost-button" disabled={saving} onClick={() => onChecklist(application, item, { isCompleted: true, isWaived: false, notes: item.notes || "" })}>Complete</button>
                    <button className="ghost-button" disabled={saving} onClick={() => onChecklist(application, item, { isCompleted: false, isWaived: true, notes: item.notes || "Waived by reviewer." })}>Waive</button>
                    <button className="ghost-button danger-text" disabled={saving} onClick={() => onChecklist(application, item, { isCompleted: false, isWaived: false, notes: "" })}>Reset</button>
                  </div>
                </div>
              ))}
            </div>
          </section>
          <section className="drawer-section">
            <div className="section-heading"><h3>Admission Documents</h3><span>{documents.filter((item) => item.documentId).length}/{documents.length}</span></div>
            {archived && <p className="drawer-permission-note">Restore this lead before uploading or reviewing documents.</p>}
            {!canManageLeads && <p className="drawer-permission-note">Read-only users can view and download uploaded documents only.</p>}
            {documents.length === 0 && <p className="muted-text">No document checklist is configured.</p>}
            <div className="document-list">
              {documents.map((document) => {
                const form = documentForms[document.documentTypeId] || {};
                const hasFile = Boolean(document.documentId);
                const canReplaceThisDocument = canUploadDocuments && (!hasFile || document.status !== "Verified" || canReviewDocuments);

                return (
                  <div className="document-row application-document-row" key={document.documentTypeId}>
                    <div className="document-main">
                      <div className="document-title-row">
                        <strong>{document.name}</strong>
                        <Badge label={document.status === "Uploaded" ? "Pending Review" : document.status} muted={!hasFile} danger={document.status === "Rejected"} />
                      </div>
                      <small>{document.isRequired ? "Required" : "Optional"}{document.fileName ? ` · ${document.fileName}` : ""}</small>
                      {document.notes && <p>{document.notes}</p>}
                      {document.uploadedAt && <small>Uploaded {formatFollowUpLabel(document.uploadedAt)}{document.uploadedBy ? ` by ${document.uploadedBy}` : ""}</small>}
                      {document.reviewedAt && <small>Reviewed {formatFollowUpLabel(document.reviewedAt)}{document.reviewedBy ? ` by ${document.reviewedBy}` : ""}</small>}
                    </div>
                    <div className="document-actions">
                      {hasFile && document.canDownload && (
                        <button type="button" className="ghost-button" disabled={saving} onClick={() => onDownloadDocument(application, document)}>
                          <Download size={16} />
                          Download
                        </button>
                      )}
                      {canReviewDocuments && hasFile && document.status !== "Verified" && (
                        <button type="button" className="ghost-button" disabled={saving} onClick={() => onVerifyDocument(application, document)}>
                          <CheckCircle2 size={16} />
                          Verify
                        </button>
                      )}
                      {canReviewDocuments && hasFile && (
                        <button type="button" className="ghost-button danger-text" disabled={saving || !form.rejectNotes?.trim()} onClick={() => handleDocumentReject(document)}>
                          <X size={16} />
                          Reject
                        </button>
                      )}
                      {canReviewDocuments && hasFile && document.status !== "Verified" && (
                        <button type="button" className="ghost-button danger-text" disabled={saving} onClick={() => onDeleteDocument(application, document)}>
                          <UserX size={16} />
                          Delete
                        </button>
                      )}
                    </div>
                    {canReviewDocuments && hasFile && (
                      <Field label="Review Notes" className="span-2">
                        <input value={form.rejectNotes || ""} maxLength={500} disabled={saving} placeholder="Required when rejecting" onChange={(event) => updateDocumentForm(document.documentTypeId, { rejectNotes: event.target.value })} />
                      </Field>
                    )}
                    {canReplaceThisDocument && (
                      <form className="document-upload-form span-2" onSubmit={(event) => handleDocumentUpload(event, document)}>
                        <Field label={hasFile ? "Replace File" : "Upload File"}>
                          <input type="file" disabled={saving} accept=".pdf,.jpg,.jpeg,.png" onChange={(event) => updateDocumentForm(document.documentTypeId, { file: event.target.files?.[0] || null })} />
                        </Field>
                        <Field label="Upload Notes">
                          <input value={form.notes || ""} maxLength={500} disabled={saving} placeholder="Optional note" onChange={(event) => updateDocumentForm(document.documentTypeId, { notes: event.target.value })} />
                        </Field>
                        <button type="submit" className="primary-button" disabled={saving || !form.file}>
                          {hasFile ? "Replace" : "Upload"}
                        </button>
                      </form>
                    )}
                    {!canReplaceThisDocument && canUploadDocuments && document.status === "Verified" && (
                      <p className="drawer-permission-note span-2">Only reviewers can replace verified documents.</p>
                    )}
                  </div>
                );
              })}
            </div>
          </section>
          <section className="drawer-section">
            <div className="section-heading">
              <h3>Fee Ledger</h3>
              <span>{formatCurrency(paymentSummary.balance || 0, paymentSummary.currency || "INR")}</span>
            </div>
            <div className="fee-summary-grid">
              <InfoItem label="Total Due" value={formatCurrency(paymentSummary.totalDue || 0, paymentSummary.currency || "INR")} />
              <InfoItem label="Paid" value={formatCurrency(paymentSummary.totalPaid || 0, paymentSummary.currency || "INR")} />
              <InfoItem label="Balance" value={formatCurrency(paymentSummary.balance || 0, paymentSummary.currency || "INR")} />
              <InfoItem label="Receipt Document" value={paymentSummary.receiptDocumentVerified ? "Verified" : "Not verified"} />
            </div>
            {!paymentSummary.paymentsReady && (
              <div className="application-blocker-note">
                <strong>Payment readiness blocked</strong>
                <span>{formatCurrency(paymentSummary.balance || 0, paymentSummary.currency || "INR")} balance remains before approval/enrollment.</span>
              </div>
            )}
            {archived && <p className="drawer-permission-note">Restore this lead before adding or updating payments.</p>}
            {!canManagePayments && <p className="drawer-permission-note">Payment management is limited to owner, admin, and accountant roles.</p>}
            {application.status === "Enrolled" && <p className="drawer-permission-note">This application is enrolled. Fee ledger is read-only here.</p>}
            {canManageApplicationPayments && (
              <form className="payment-form" onSubmit={handlePaymentCreate}>
                <Field label="Fee Item" required>
                  <input value={paymentForm.title} maxLength={160} onChange={(event) => setPaymentForm((current) => ({ ...current, title: event.target.value }))} required />
                </Field>
                <Field label="Amount Due" required>
                  <input type="number" min="0.01" step="0.01" value={paymentForm.amountDue} onChange={(event) => setPaymentForm((current) => ({ ...current, amountDue: event.target.value }))} required />
                </Field>
                <Field label="Due Date">
                  <input type="date" value={paymentForm.dueDate} onChange={(event) => setPaymentForm((current) => ({ ...current, dueDate: event.target.value }))} />
                </Field>
                <Field label="Notes">
                  <input value={paymentForm.notes} maxLength={500} onChange={(event) => setPaymentForm((current) => ({ ...current, notes: event.target.value }))} />
                </Field>
                <button type="submit" className="primary-button" disabled={!paymentForm.title.trim() || !paymentForm.amountDue || Number(paymentForm.amountDue) <= 0}>
                  {saving ? "Saving..." : "Add Fee"}
                </button>
              </form>
            )}
            <div className="payment-list">
              {payments.length === 0 && <p className="muted-text">No fee items added yet.</p>}
              {payments.map((payment) => {
                const transactionForm = paymentTransactionForms[payment.id] || {};
                const editable = canManageApplicationPayments && payment.status !== "Cancelled";
                const canReceive = editable && Number(payment.balance || 0) > 0;
                const canCancel = editable && Number(payment.amountPaid || 0) <= 0;
                return (
                  <div className="payment-row" key={payment.id}>
                    <div className="payment-main">
                      <div className="document-title-row">
                        <strong>{payment.title}</strong>
                        <Badge label={payment.status} muted={payment.status === "Pending"} danger={payment.status === "Overdue"} />
                      </div>
                      <div className="payment-amount-grid">
                        <InfoItem label="Due" value={formatCurrency(payment.amountDue, payment.currency)} />
                        <InfoItem label="Paid" value={formatCurrency(payment.amountPaid, payment.currency)} />
                        <InfoItem label="Balance" value={formatCurrency(payment.balance, payment.currency)} />
                      </div>
                      {payment.dueDate && <small>Due {formatDate(payment.dueDate)}</small>}
                      {payment.notes && <small>{payment.notes}</small>}
                    </div>
                    <div className="document-actions">
                      {editable && (
                        <button type="button" className="ghost-button" disabled={saving} onClick={() => openPaymentEdit(payment)}>
                          <Pencil size={16} />
                          Edit
                        </button>
                      )}
                      {canCancel && (
                        <button type="button" className="ghost-button danger-text" disabled={saving} onClick={() => onCancelPayment(application, payment)}>
                          <X size={16} />
                          Cancel
                        </button>
                      )}
                    </div>
                    {editingPaymentId === payment.id && editingPaymentForm && (
                      <form className="payment-form span-all" onSubmit={handlePaymentUpdate}>
                        <Field label="Fee Item" required>
                          <input value={editingPaymentForm.title} maxLength={160} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, title: event.target.value }))} required />
                        </Field>
                        <Field label="Amount Due" required>
                          <input type="number" min={Math.max(Number(payment.amountPaid || 0), 0.01)} step="0.01" value={editingPaymentForm.amountDue} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, amountDue: event.target.value }))} required />
                        </Field>
                        <Field label="Due Date">
                          <input type="date" value={editingPaymentForm.dueDate} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, dueDate: event.target.value }))} />
                        </Field>
                        <Field label="Notes">
                          <input value={editingPaymentForm.notes} maxLength={500} onChange={(event) => setEditingPaymentForm((current) => ({ ...current, notes: event.target.value }))} />
                        </Field>
                        <div className="inline-editor-actions">
                          <button type="button" className="ghost-button" disabled={saving} onClick={() => { setEditingPaymentId(""); setEditingPaymentForm(null); }}>Cancel</button>
                          <button type="submit" className="primary-button" disabled={saving || !editingPaymentForm.title.trim() || Number(editingPaymentForm.amountDue) < Number(payment.amountPaid || 0)}>Save</button>
                        </div>
                      </form>
                    )}
                    {canReceive && (
                      <form className="payment-transaction-form span-all" onSubmit={(event) => handlePaymentTransactionCreate(event, payment)}>
                        <Field label="Receive Amount" required>
                          <input type="number" min="0.01" max={payment.balance} step="0.01" value={transactionForm.amount || ""} onChange={(event) => updatePaymentTransactionForm(payment.id, { amount: event.target.value })} required />
                        </Field>
                        <Field label="Method">
                          <select value={transactionForm.method || "UPI"} onChange={(event) => updatePaymentTransactionForm(payment.id, { method: event.target.value })}>
                            {["UPI", "Cash", "Bank Transfer", "Card", "Cheque", "Other"].map((item) => <option key={item} value={item}>{item}</option>)}
                          </select>
                        </Field>
                        <Field label="Paid At">
                          <input type="datetime-local" value={transactionForm.paidAt || ""} onChange={(event) => updatePaymentTransactionForm(payment.id, { paidAt: event.target.value })} />
                        </Field>
                        <Field label="Reference">
                          <input value={transactionForm.referenceNumber || ""} maxLength={120} onChange={(event) => updatePaymentTransactionForm(payment.id, { referenceNumber: event.target.value })} />
                        </Field>
                        <Field label="Receipt">
                          <input value={transactionForm.receiptNumber || ""} maxLength={120} placeholder="Auto-generated if blank" onChange={(event) => updatePaymentTransactionForm(payment.id, { receiptNumber: event.target.value })} />
                        </Field>
                        <Field label="Notes">
                          <input value={transactionForm.notes || ""} maxLength={500} onChange={(event) => updatePaymentTransactionForm(payment.id, { notes: event.target.value })} />
                        </Field>
                        <button type="submit" className="primary-button" disabled={saving || !transactionForm.amount || Number(transactionForm.amount) <= 0 || Number(transactionForm.amount) > Number(payment.balance || 0)}>
                          {saving ? "Saving..." : "Record Payment"}
                        </button>
                      </form>
                    )}
                    {payment.transactions.length > 0 && (
                      <div className="payment-transactions span-all">
                        {payment.transactions.map((transaction) => (
                          <div className="payment-transaction" key={transaction.id}>
                            <strong>{formatCurrency(transaction.amount, payment.currency)}</strong>
                            <span>{transaction.method}</span>
                            <span>{formatFollowUpLabel(transaction.paidAt)}</span>
                            <span>{transaction.receiptNumber || "No receipt"}</span>
                            {transaction.referenceNumber && <span>{transaction.referenceNumber}</span>}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </section>
          <section className="drawer-section">
            <div className="section-heading"><h3>Status Timeline</h3><span>{application.statusHistory.length}</span></div>
            <div className="timeline">
              {application.statusHistory.map((item, index) => (
                <div className="timeline-item" key={`${item.newStatus}-${item.changedAt}-${index}`}>
                  <span className="timeline-dot note" />
                  <div>
                    <strong>{formatApplicationStatus(item.newStatus)}</strong>
                    <p>{item.note || "Status changed."}</p>
                    <small>{formatFollowUpLabel(item.changedAt)} · {item.changedBy}</small>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </div>
      </aside>
    </div>
  );
}

function formatApplicationStatus(status) {
  const labels = {
    UnderReview: "Under Review",
    ChangesRequired: "Changes Required",
  };
  return labels[status] || status || "Unknown";
}

function CounsellorWorkInsights({ reports, options, filters, status, onFiltersChange, onResetFilters, onRetry, onOpenLead }) {
  const hasFilters = JSON.stringify(filters) !== JSON.stringify(defaultReportFilters());
  const followUps = reports?.followUps || {};
  const outcomes = reports?.outcomes || {};
  const pipelineTotal = (reports?.pipeline || []).reduce((sum, item) => sum + item.totalLeads, 0);

  return (
    <>
      <PageTitle
        title="My Work Insights"
        subtitle="A focused view of who needs attention and how your admission follow-ups are progressing."
      />

      <div className="report-filter-panel counsellor-insight-filters">
        <Field label="Start Date"><input type="date" value={filters.startDate} onChange={(event) => onFiltersChange({ startDate: event.target.value })} /></Field>
        <Field label="End Date"><input type="date" value={filters.endDate} onChange={(event) => onFiltersChange({ endDate: event.target.value })} /></Field>
        <Field label="Course">
          <select value={filters.courseId} onChange={(event) => onFiltersChange({ courseId: event.target.value })}>
            <option value="">All courses</option>
            {options.courses.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <Field label="Source">
          <select value={filters.sourceId} onChange={(event) => onFiltersChange({ sourceId: event.target.value })}>
            <option value="">All sources</option>
            {options.sources.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </Field>
        <div className="lead-filter-actions">
          <strong>{status.loading ? "Refreshing..." : reports ? `${reports.startDate} to ${reports.endDate}` : "No insights loaded"}</strong>
          <button className="soft-button" type="button" onClick={onResetFilters} disabled={!hasFilters || status.loading}>Reset</button>
        </div>
      </div>

      {status.error && <div className="form-alert" role="alert">{status.error}</div>}
      {status.loading && !reports && <StatePanel title="Preparing your work insights" message="Checking your assigned leads and follow-ups..." />}
      {!status.loading && !status.error && !reports && <StatePanel title="No insights loaded" message="Load your current admission workload." action={onRetry} actionLabel="Load insights" />}

      {reports && (
        <div className="counsellor-insights">
          <section>
            <div className="section-heading insight-heading">
              <div><h2>Needs attention</h2><p>Work from left to right. Each card contains the highest-priority leads first.</p></div>
            </div>
            <div className="attention-grid">
              {reports.attention.map((group) => (
                <details className={`attention-card ${group.count > 0 ? "has-items" : ""}`} key={group.key} open={group.key === "overdue" && group.count > 0}>
                  <summary>
                    <span>{group.title}</span>
                    <strong>{formatNumber(group.count)}</strong>
                    <small>{group.guidance}</small>
                  </summary>
                  <div className="attention-list">
                    {group.items.length === 0 && <p>Nothing needs attention here.</p>}
                    {group.items.map((lead) => (
                      <button type="button" key={lead.id} onClick={() => onOpenLead(lead.id)}>
                        <span><strong>{lead.studentName}</strong><small>{lead.course} · {lead.stage}</small></span>
                        <Badge label={lead.priority} danger={["High", "Urgent"].includes(lead.priority)} />
                      </button>
                    ))}
                    {group.count > group.items.length && <small>Showing the first {group.items.length} of {group.count}. Use Leads filters for the full list.</small>}
                  </div>
                </details>
              ))}
            </div>
          </section>

          <section className="insight-two-column">
            <Card title="My pipeline health" className="insight-panel">
              {reports.pipeline.length === 0 && <StatePanel title="No assigned leads" message="No leads match these filters." />}
              <div className="pipeline-health-list">
                {reports.pipeline.map((stage) => (
                  <div key={stage.stageId}>
                    <div><strong>{stage.stage}</strong><span>{stage.totalLeads} leads{stage.stuckLeads > 0 ? ` · ${stage.stuckLeads} need review` : ""}</span></div>
                    <div className="pipeline-health-track"><span style={{ width: `${pipelineTotal ? Math.max(4, stage.totalLeads / pipelineTotal * 100) : 0}%` }} /></div>
                  </div>
                ))}
              </div>
              <p className="insight-note">“Need review” means the lead has remained without a recorded stage change for seven days. Legacy leads use their creation date.</p>
            </Card>

            <Card title="Follow-up discipline" className="insight-panel">
              <div className="insight-metrics">
                <Metric title="Completion Rate" value={`${followUps.completionRate || 0}%`} />
                <Metric title="Completed On Time" value={formatNumber(followUps.completedOnTime)} />
                <Metric title="Completed Late" value={formatNumber(followUps.completedLate)} warning={followUps.completedLate > 0} />
                <Metric title="Currently Overdue" value={formatNumber(followUps.currentlyOverdue)} warning={followUps.currentlyOverdue > 0} />
              </div>
              <p className="insight-note">Completion rate uses follow-ups due during the selected period. Current overdue always reflects your live workload.</p>
            </Card>
          </section>

          <section>
            <div className="section-heading insight-heading"><div><h2>Admission outcomes</h2><p>Outcomes for your current portfolio during the selected period.</p></div></div>
            <div className="outcome-grid">
              <Metric title="New Leads" value={formatNumber(outcomes.newLeads)} />
              <Metric title="Won" value={formatNumber(outcomes.wonLeads)} />
              <Metric title="Lost" value={formatNumber(outcomes.lostLeads)} warning={outcomes.lostLeads > 0} />
              <Metric title="Open Portfolio" value={formatNumber(outcomes.openLeads)} />
              <Metric title="Conversion" value={`${outcomes.conversionRate || 0}%`} />
            </div>
          </section>

          <section className="insight-two-column">
            <InsightBreakdown title="Course interest" rows={reports.courses} empty="No course activity in this period." />
            <InsightBreakdown title="Source quality" rows={reports.sources} empty="No source activity in this period." />
          </section>
        </div>
      )}
    </>
  );
}

function InsightBreakdown({ title, rows, empty }) {
  return (
    <Card title={title} className="insight-panel report-table-card">
      {rows.length === 0 && <StatePanel title="No data" message={empty} />}
      {rows.length > 0 && <div className="report-table"><table><thead><tr><th>Name</th><th>Leads</th><th>Won</th><th>Open</th><th>Conversion</th></tr></thead><tbody>
        {rows.map((item) => <tr key={item.id}><td><strong>{item.name}</strong></td><td>{item.totalLeads}</td><td>{item.wonLeads}</td><td>{item.openLeads}</td><td>{item.totalLeads ? Math.round(item.wonLeads / item.totalLeads * 100) : 0}%</td></tr>)}
      </tbody></table></div>}
    </Card>
  );
}

function ReportTable({ title, emptyTitle, emptyMessage, columns, rows, renderRow }) {
  return (
    <Card title={title} className="report-table-card wide-card">
      {rows.length === 0 && <StatePanel title={emptyTitle} message={emptyMessage} />}
      {rows.length > 0 && (
        <div className="report-table">
          <table>
            <thead>
              <tr>{columns.map((column) => <th key={column}>{column}</th>)}</tr>
            </thead>
            <tbody>{rows.map(renderRow)}</tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

const masterDataTabs = [
  { id: "branches", label: "Branches", singular: "Branch", apiPath: "branches", icon: Building2 },
  { id: "courses", label: "Courses", singular: "Course", apiPath: "courses", icon: BookOpen },
  { id: "sources", label: "Lead Sources", singular: "Lead Source", apiPath: "lead-sources", icon: Radio },
  { id: "stages", label: "Pipeline Stages", singular: "Lead Stage", apiPath: "lead-stages", icon: GitBranch },
];

const profileTab = { id: "profile", label: "Institute Profile", singular: "Profile", icon: Building2 };
const templateTab = { id: "templates", label: "Communication Templates", singular: "Template", icon: FileText };
const settingsTabs = [profileTab, ...masterDataTabs, templateTab];
const supportedTimeZones = [
  ["Asia/Kolkata", "India Standard Time (UTC+05:30)"],
  ["Asia/Dubai", "Dubai (UTC+04:00)"],
  ["Asia/Singapore", "Singapore (UTC+08:00)"],
  ["Asia/Tokyo", "Tokyo (UTC+09:00)"],
  ["Asia/Kathmandu", "Kathmandu (UTC+05:45)"],
  ["Asia/Dhaka", "Dhaka (UTC+06:00)"],
  ["Asia/Colombo", "Colombo (UTC+05:30)"],
  ["Europe/London", "London"],
  ["Europe/Berlin", "Berlin"],
  ["America/New_York", "New York"],
  ["America/Chicago", "Chicago"],
  ["America/Denver", "Denver"],
  ["America/Los_Angeles", "Los Angeles"],
  ["Australia/Sydney", "Sydney"],
  ["UTC", "UTC"],
];
const templateChannels = ["Call", "WhatsApp", "Email", "SMS", "Meeting", "Note"];
const templatePlaceholders = [
  "studentName",
  "leadNumber",
  "course",
  "stage",
  "status",
  "priority",
  "source",
  "counsellor",
  "phone",
  "email",
  "city",
  "tenantName",
  "nextFollowUp",
];

function SettingsPage({ currentUser, onMasterDataChanged, onTenantProfileChanged }) {
  const [masterData, setMasterData] = useState({ branches: [], courses: [], sources: [], stages: [] });
  const [templates, setTemplates] = useState([]);
  const [tenantProfile, setTenantProfile] = useState(null);
  const [tenantUsers, setTenantUsers] = useState([]);
  const [profileDirty, setProfileDirty] = useState(false);
  const [activeTab, setActiveTab] = useState("profile");
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [editor, setEditor] = useState(null);
  const [status, setStatus] = useState({ loading: true, saving: false, error: "", fieldErrors: {}, message: "" });
  const canManage = ["Owner", "Admin"].includes(currentUser.role);
  const tab = settingsTabs.find((item) => item.id === activeTab) || settingsTabs[0];
  const isProfileTab = activeTab === profileTab.id;
  const isTemplateTab = activeTab === templateTab.id;

  const loadConfiguration = useCallback(async (silent = false) => {
    if (!silent) {
      setStatus((current) => ({ ...current, loading: true, error: "", message: "" }));
    }
    try {
      const [profileResponse, response, templateResponse, userResponse] = await Promise.all([
        getCurrentTenant(),
        getMasterData(),
        getCommunicationTemplates({ status: "all" }),
        getUsers(),
      ]);
      setTenantProfile(profileResponse);
      setMasterData(response);
      setTemplates(templateResponse);
      setTenantUsers(userResponse);
      setStatus((current) => ({ ...current, loading: false, error: "" }));
      return true;
    } catch (error) {
      setStatus((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : "Unable to load settings.",
      }));
      return false;
    }
  }, []);

  useEffect(() => {
    loadConfiguration();
  }, [loadConfiguration]);

  const records = isProfileTab ? [] : isTemplateTab ? templates : masterData[activeTab] || [];
  const visibleRecords = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    return records.filter((record) => {
      const searchText = isTemplateTab
        ? `${record.name} ${record.channel || ""} ${record.category || ""} ${record.body || ""}`.toLocaleLowerCase()
        : `${record.name} ${record.city || ""}`.toLocaleLowerCase();
      const matchesQuery = !normalizedQuery || searchText.includes(normalizedQuery);
      const matchesStatus = statusFilter === "all" || (statusFilter === "active" ? record.isActive : !record.isActive);
      return matchesQuery && matchesStatus;
    });
  }, [isTemplateTab, query, records, statusFilter]);

  const saveTenantProfile = async (payload) => {
    setStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {}, message: "" }));
    try {
      const response = await updateCurrentTenant(payload);
      setTenantProfile(response);
      setProfileDirty(false);
      setStatus({
        loading: false,
        saving: false,
        error: "",
        fieldErrors: {},
        message: "Institute profile saved.",
      });
      onTenantProfileChanged?.(response);
      return true;
    } catch (error) {
      setStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to save the institute profile.",
        fieldErrors: error?.errors || {},
        message: "",
      }));
      return false;
    }
  };

  const clearActionStatus = () => {
    setStatus((current) => ({ ...current, error: "", fieldErrors: {}, message: "" }));
  };

  const openEditor = (record = null) => {
    clearActionStatus();
    setEditor({ type: activeTab, record });
  };

  const refreshConfiguration = async () => {
    const [refreshed, refreshedTemplates] = await Promise.all([
      getMasterData(),
      getCommunicationTemplates({ status: "all" }),
    ]);
    setMasterData(refreshed);
    setTemplates(refreshedTemplates);
  };

  const saveTemplate = async (record, payload) => {
    setStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {}, message: "" }));
    try {
      const response = record
        ? await updateCommunicationTemplate(record.id, payload)
        : await createCommunicationTemplate(payload);
      await refreshConfiguration();
      setEditor(null);
      setStatus({
        loading: false,
        saving: false,
        error: "",
        fieldErrors: {},
        message: response.message || "Communication template saved.",
      });
      onMasterDataChanged?.();
      return true;
    } catch (error) {
      setStatus((current) => ({
        ...current,
        saving: false,
        error: error instanceof Error ? error.message : "Unable to save communication template.",
        fieldErrors: error?.errors || {},
        message: "",
      }));
      return false;
    }
  };

  const saveRecord = async (type, record, payload) => {
    const config = masterDataTabs.find((item) => item.id === type);
    if (!config) {
      return saveTemplate(record, payload);
    }
    setStatus((current) => ({ ...current, saving: true, error: "", fieldErrors: {}, message: "" }));
    try {
      const response = record
        ? await updateMasterRecord(config.apiPath, record.id, payload)
        : await createMasterRecord(config.apiPath, payload);
      await refreshConfiguration();
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
      await refreshConfiguration();
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
        title="Settings"
        subtitle="Manage the institute profile, lead configuration, and communication templates."
        action={canManage && !isProfileTab ? (
          <button className="primary-button" type="button" onClick={() => openEditor()} disabled={status.loading || status.saving}>
            <Plus size={18} />Add {tab.singular}
          </button>
        ) : null}
      />

      <nav className="master-tabs" aria-label="Settings categories">
        {settingsTabs.map((item) => {
          const Icon = item.icon;
          const itemRecords = item.id === profileTab.id ? [] : item.id === templateTab.id ? templates : masterData[item.id] || [];
          return (
            <button
              key={item.id}
              type="button"
              className={activeTab === item.id ? "active" : ""}
              onClick={() => {
                if (activeTab === profileTab.id && item.id !== profileTab.id && profileDirty &&
                    !window.confirm("Discard the unsaved institute profile changes?")) {
                  return;
                }
                setProfileDirty(false);
                setActiveTab(item.id);
                setQuery("");
                setStatusFilter("all");
                clearActionStatus();
              }}
            >
              <Icon size={18} />
              <span>{item.label}</span>
              {item.id !== profileTab.id && <strong>{itemRecords.length}</strong>}
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
          <button className="soft-button" type="button" onClick={() => loadConfiguration()}><RotateCcw size={17} />Retry</button>
        </div>
      )}

      {isProfileTab && status.loading && <StatePanel title="Loading institute profile" message="Fetching institute details and branding..." />}
      {isProfileTab && !status.loading && tenantProfile && (
        <TenantProfilePanel
          profile={tenantProfile}
          branches={masterData.branches}
          users={tenantUsers}
          canManage={canManage}
          saving={status.saving}
          fieldErrors={status.fieldErrors}
          onDirtyChange={setProfileDirty}
          onSubmit={saveTenantProfile}
        />
      )}

      {!isProfileTab && !status.loading && (
        <div className="master-summary">
          <span><strong>{records.length}</strong> total</span>
          <span><strong>{activeCount}</strong> active</span>
          <span><strong>{records.length - activeCount}</strong> inactive</span>
          {!canManage && <span className="read-only-note">Read-only access</span>}
        </div>
      )}

      {!isProfileTab && !status.loading && records.length > 0 && (
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

      {!isProfileTab && <div className="table-card master-table">
        {status.loading && <StatePanel title="Loading master data" message="Fetching tenant configuration..." />}
        {!status.loading && records.length === 0 && <StatePanel title={`No ${tab.label.toLowerCase()}`} message={`Add the first ${tab.singular.toLowerCase()} to this workspace.`} />}
        {!status.loading && records.length > 0 && visibleRecords.length === 0 && (
          <StatePanel title="No matching records" message="Change or clear the current filters." action={() => { setQuery(""); setStatusFilter("all"); }} />
        )}
        {!status.loading && visibleRecords.length > 0 && (
          <table>
            <thead>
              <tr>
                {!isTemplateTab && activeTab === "stages" && <th>Order</th>}
                <th>{tab.singular}</th>
                {!isTemplateTab && activeTab === "branches" && <th>City</th>}
                {isTemplateTab ? <th>Channel</th> : <th>Usage</th>}
                {isTemplateTab && <th>Category</th>}
                {!isTemplateTab && activeTab === "stages" && <th>Type</th>}
                <th>Status</th>
                {isTemplateTab && <th>Version</th>}
                <th>Updated</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleRecords.map((record) => {
                const activeStages = masterData.stages.filter((item) => item.isActive).sort((left, right) => left.sortOrder - right.sortOrder);
                const stageIndex = !isTemplateTab && activeTab === "stages" ? activeStages.findIndex((item) => item.id === record.id) : -1;
                return (
                  <tr key={record.id}>
                    {!isTemplateTab && activeTab === "stages" && (
                      <td>
                        {canManage && record.isActive ? (
                          <div className="stage-order-actions">
                            <button className="icon-button table-action-button" type="button" onClick={() => moveStage(record, -1)} disabled={status.saving || stageIndex <= 0} aria-label={`Move ${record.name} up`} title="Move up"><ArrowUp size={16} /></button>
                            <button className="icon-button table-action-button" type="button" onClick={() => moveStage(record, 1)} disabled={status.saving || stageIndex < 0 || stageIndex >= activeStages.length - 1} aria-label={`Move ${record.name} down`} title="Move down"><ArrowDown size={16} /></button>
                          </div>
                        ) : <span className="team-access-label">{record.isActive ? stageIndex + 1 : "-"}</span>}
                      </td>
                    )}
                    <td>
                      <strong>{record.name}</strong>
                      {isTemplateTab && <small className="template-table-preview">{record.body}</small>}
                    </td>
                    {!isTemplateTab && activeTab === "branches" && <td>{record.city}</td>}
                    <td>{isTemplateTab ? <Badge label={record.channel} /> : activeTab === "branches" ? `${record.activeUsers} users / ${record.leads} leads` : `${record.leads} leads`}</td>
                    {isTemplateTab && <td>{record.category || "-"}</td>}
                    {!isTemplateTab && activeTab === "stages" && (
                      <td><StageTypeBadges stage={record} /></td>
                    )}
                    <td><Badge label={record.isActive ? "Active" : "Inactive"} muted={!record.isActive} /></td>
                    {isTemplateTab && <td>v{record.version}</td>}
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
      </div>}

      {editor && editor.type !== templateTab.id && (
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
      {editor?.type === templateTab.id && (
        <TemplateModal
          key={`${editor.record?.id || "new"}-template`}
          record={editor.record}
          saving={status.saving}
          error={status.error}
          fieldErrors={status.fieldErrors}
          onClose={() => { if (!status.saving) { setEditor(null); clearActionStatus(); } }}
          onSubmit={(payload) => saveTemplate(editor.record, payload)}
        />
      )}
    </>
  );
}

function tenantProfileForm(profile) {
  return {
    name: profile.name || "",
    contactEmail: profile.contactEmail || "",
    contactPhone: profile.contactPhone || "",
    websiteUrl: profile.websiteUrl || "",
    addressLine1: profile.addressLine1 || "",
    addressLine2: profile.addressLine2 || "",
    city: profile.city || "",
    state: profile.state || "",
    postalCode: profile.postalCode || "",
    country: profile.country || "India",
    timeZone: profile.timeZone || "Asia/Kolkata",
    logoUrl: profile.logoUrl || "",
    brandColor: profile.brandColor || "#2171D3",
    defaultBranchId: profile.defaultBranchId || "",
    defaultAssigneeUserId: profile.defaultAssigneeUserId || "",
  };
}

function TenantProfilePanel({ profile, branches, users, canManage, saving, fieldErrors, onDirtyChange, onSubmit }) {
  const [form, setForm] = useState(() => tenantProfileForm(profile));
  const [clientErrors, setClientErrors] = useState({});
  const [logoFailed, setLogoFailed] = useState(false);
  const initialForm = useMemo(() => tenantProfileForm(profile), [profile]);
  const dirty = JSON.stringify(form) !== JSON.stringify(initialForm);
  const getFieldError = (field) => clientErrors[field] || firstError(fieldErrors[field]);

  useEffect(() => {
    setForm(tenantProfileForm(profile));
    setClientErrors({});
    setLogoFailed(false);
  }, [profile]);

  useEffect(() => {
    onDirtyChange(dirty);
  }, [dirty, onDirtyChange]);

  useEffect(() => () => onDirtyChange(false), [onDirtyChange]);

  useEffect(() => {
    if (!dirty) {
      return undefined;
    }
    const warnBeforeUnload = (event) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnBeforeUnload);
    return () => window.removeEventListener("beforeunload", warnBeforeUnload);
  }, [dirty]);

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setClientErrors((current) => ({ ...current, [field]: "" }));
    if (field === "logoUrl") {
      setLogoFailed(false);
    }
  };

  const validate = () => {
    const errors = {};
    if (!form.name.trim()) errors.name = "Institute name is required.";
    if (form.name.trim().length > 160) errors.name = "Maximum length is 160 characters.";
    if (form.contactEmail.trim() && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.contactEmail.trim())) errors.contactEmail = "Enter a valid contact email address.";
    if (form.contactPhone.trim()) {
      const digits = form.contactPhone.replace(/\D/g, "");
      if (!/^[0-9+()\-\s.]+$/.test(form.contactPhone.trim()) || digits.length < 7 || digits.length > 15) errors.contactPhone = "Enter a valid phone number containing 7 to 15 digits.";
    }
    ["websiteUrl", "logoUrl"].forEach((field) => {
      if (!form[field].trim()) return;
      try {
        const url = new URL(form[field].trim());
        if (!["http:", "https:"].includes(url.protocol)) throw new Error("Invalid protocol");
      } catch {
        errors[field] = `Enter a valid HTTP or HTTPS ${field === "logoUrl" ? "logo URL" : "website URL"}.`;
      }
    });
    if (!form.country.trim()) errors.country = "Country is required.";
    if (!supportedTimeZones.some(([value]) => value === form.timeZone)) errors.timeZone = "Select a supported timezone.";
    if (!/^#[0-9A-Fa-f]{6}$/.test(form.brandColor.trim())) errors.brandColor = "Enter a six-digit hex color such as #2171D3.";
    const defaultBranch = branches.find((item) => item.id === form.defaultBranchId);
    if (form.defaultBranchId && (!defaultBranch || !defaultBranch.isActive)) errors.defaultBranchId = "Select a valid active branch.";
    const defaultAssignee = users.find((item) => item.id === form.defaultAssigneeUserId);
    if (form.defaultAssigneeUserId && (!defaultAssignee || !defaultAssignee.isActive || ["Accountant", "ReadOnly"].includes(defaultAssignee.role))) {
      errors.defaultAssigneeUserId = "Select an active CRM user.";
    } else if (defaultBranch && defaultAssignee?.branchId && defaultAssignee.branchId !== defaultBranch.id) {
      errors.defaultAssigneeUserId = "The default assignee must belong to the selected default branch.";
    }
    setClientErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!canManage || saving || !dirty || !validate()) {
      return;
    }
    await onSubmit({
      ...form,
      name: form.name.trim(),
      contactEmail: form.contactEmail.trim(),
      contactPhone: form.contactPhone.trim(),
      websiteUrl: form.websiteUrl.trim(),
      addressLine1: form.addressLine1.trim(),
      addressLine2: form.addressLine2.trim(),
      city: form.city.trim(),
      state: form.state.trim(),
      postalCode: form.postalCode.trim(),
      country: form.country.trim(),
      logoUrl: form.logoUrl.trim(),
      brandColor: form.brandColor.trim().toUpperCase(),
      defaultBranchId: form.defaultBranchId || null,
      defaultAssigneeUserId: form.defaultAssigneeUserId || null,
      version: profile.version,
    });
  };

  const controlsDisabled = !canManage || saving;

  return (
    <form className="tenant-profile" onSubmit={handleSubmit} noValidate>
      <header className="tenant-profile-header">
        <div className="tenant-logo-preview" style={{ borderColor: form.brandColor }}>
          {form.logoUrl && !logoFailed
            ? <img src={form.logoUrl} alt={`${form.name || "Institute"} logo preview`} onError={() => setLogoFailed(true)} />
            : <span style={{ background: form.brandColor }}>{initials(form.name || profile.slug).slice(0, 2)}</span>}
        </div>
        <div>
          <h2>{form.name || "Institute profile"}</h2>
          <p>{profile.slug}</p>
        </div>
        <div className="tenant-profile-meta">
          <Badge label={profile.isActive ? "Active" : "Inactive"} muted={!profile.isActive} />
          <span>Version {profile.version}</span>
        </div>
      </header>

      {!canManage && <div className="team-notice"><span>Only owners and admins can edit this profile.</span></div>}

      <section className="tenant-profile-section">
        <h3>Institute details</h3>
        <div className="form-grid">
          <Field label="Institute Name" error={getFieldError("name")} required>
            <input value={form.name} onChange={(event) => updateField("name", event.target.value)} maxLength={160} disabled={controlsDisabled} />
          </Field>
          <Field label="Workspace Slug">
            <input value={profile.slug} disabled aria-readonly="true" />
          </Field>
          <Field label="Contact Email" error={getFieldError("contactEmail")}>
            <input type="email" value={form.contactEmail} onChange={(event) => updateField("contactEmail", event.target.value)} maxLength={240} disabled={controlsDisabled} />
          </Field>
          <Field label="Contact Phone" error={getFieldError("contactPhone")}>
            <input type="tel" value={form.contactPhone} onChange={(event) => updateField("contactPhone", event.target.value)} maxLength={40} disabled={controlsDisabled} />
          </Field>
          <Field label="Website URL" error={getFieldError("websiteUrl")} className="span-2">
            <input type="url" value={form.websiteUrl} onChange={(event) => updateField("websiteUrl", event.target.value)} maxLength={500} placeholder="https://www.example.edu" disabled={controlsDisabled} />
          </Field>
        </div>
      </section>

      <section className="tenant-profile-section">
        <h3>Address and timezone</h3>
        <div className="form-grid">
          <Field label="Address Line 1" error={getFieldError("addressLine1")} className="span-2">
            <input value={form.addressLine1} onChange={(event) => updateField("addressLine1", event.target.value)} maxLength={200} disabled={controlsDisabled} />
          </Field>
          <Field label="Address Line 2" error={getFieldError("addressLine2")} className="span-2">
            <input value={form.addressLine2} onChange={(event) => updateField("addressLine2", event.target.value)} maxLength={200} disabled={controlsDisabled} />
          </Field>
          <Field label="City" error={getFieldError("city")}>
            <input value={form.city} onChange={(event) => updateField("city", event.target.value)} maxLength={120} disabled={controlsDisabled} />
          </Field>
          <Field label="State / Province" error={getFieldError("state")}>
            <input value={form.state} onChange={(event) => updateField("state", event.target.value)} maxLength={120} disabled={controlsDisabled} />
          </Field>
          <Field label="Postal Code" error={getFieldError("postalCode")}>
            <input value={form.postalCode} onChange={(event) => updateField("postalCode", event.target.value)} maxLength={20} disabled={controlsDisabled} />
          </Field>
          <Field label="Country" error={getFieldError("country")} required>
            <input value={form.country} onChange={(event) => updateField("country", event.target.value)} maxLength={80} disabled={controlsDisabled} />
          </Field>
          <Field label="Timezone" error={getFieldError("timeZone")} className="span-2" required>
            <select value={form.timeZone} onChange={(event) => updateField("timeZone", event.target.value)} disabled={controlsDisabled}>
              {supportedTimeZones.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          </Field>
        </div>
      </section>

      <section className="tenant-profile-section">
        <h3>Branding</h3>
        <div className="form-grid">
          <Field label="Logo URL" error={getFieldError("logoUrl")} className="span-2">
            <input type="url" value={form.logoUrl} onChange={(event) => updateField("logoUrl", event.target.value)} maxLength={500} placeholder="https://cdn.example.edu/logo.png" disabled={controlsDisabled} />
          </Field>
          <Field label="Brand Color" error={getFieldError("brandColor")} required>
            <div className="color-input-group">
              <input type="color" value={/^#[0-9A-Fa-f]{6}$/.test(form.brandColor) ? form.brandColor : "#2171D3"} onChange={(event) => updateField("brandColor", event.target.value.toUpperCase())} disabled={controlsDisabled} aria-label="Select brand color" />
              <input value={form.brandColor} onChange={(event) => updateField("brandColor", event.target.value)} maxLength={7} disabled={controlsDisabled} />
            </div>
          </Field>
        </div>
      </section>

      <section className="tenant-profile-section">
        <h3>Lead defaults</h3>
        <div className="form-grid">
          <Field label="Default Branch" error={getFieldError("defaultBranchId")}>
            <select value={form.defaultBranchId} onChange={(event) => updateField("defaultBranchId", event.target.value)} disabled={controlsDisabled}>
              <option value="">No default branch</option>
              {branches.map((item) => <option key={item.id} value={item.id} disabled={!item.isActive}>{item.name}{item.isActive ? "" : " (Inactive)"}</option>)}
            </select>
          </Field>
          <Field label="Default Assignee" error={getFieldError("defaultAssigneeUserId")}>
            <select value={form.defaultAssigneeUserId} onChange={(event) => updateField("defaultAssigneeUserId", event.target.value)} disabled={controlsDisabled}>
              <option value="">Leave new leads unassigned</option>
              {users
                .filter((item) => !["Accountant", "ReadOnly"].includes(item.role))
                .map((item) => <option key={item.id} value={item.id} disabled={!item.isActive}>{item.fullName} ({item.role}){item.isActive ? "" : " - Inactive"}</option>)}
            </select>
          </Field>
        </div>
      </section>

      {canManage && (
        <footer className="tenant-profile-actions">
          <span>{dirty ? "Unsaved changes" : `Last updated ${formatDate(profile.updatedAt)}`}</span>
          <button className="ghost-button" type="button" onClick={() => { setForm(initialForm); setClientErrors({}); setLogoFailed(false); }} disabled={saving || !dirty}>
            <RotateCcw size={17} />Reset
          </button>
          <button className="primary-button" type="submit" disabled={saving || !dirty}>
            <CheckCircle2 size={17} />{saving ? "Saving..." : "Save Profile"}
          </button>
        </footer>
      )}
    </form>
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

function TemplateModal({ record, saving, error, fieldErrors, onClose, onSubmit }) {
  const isEditing = Boolean(record);
  const [form, setForm] = useState({
    name: record?.name || "",
    channel: record?.channel || "WhatsApp",
    category: record?.category || "",
    body: record?.body || "",
    isActive: record?.isActive ?? true,
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

  const handleSubmit = (event) => {
    event.preventDefault();
    const errors = validateCommunicationTemplateForm(form);
    setClientErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    const payload = {
      name: form.name.trim(),
      channel: form.channel,
      category: form.category.trim(),
      body: form.body.trim(),
      isActive: form.isActive,
      version: record?.version || 0,
    };

    onSubmit(payload);
  };

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && !saving && onClose()}>
      <form className="modal team-modal template-modal" onSubmit={handleSubmit} noValidate role="dialog" aria-modal="true" aria-labelledby="template-modal-title">
        <header className="modal-header">
          <div>
            <h2 id="template-modal-title">{isEditing ? "Edit" : "Add"} Communication Template</h2>
            <p>{isEditing ? `Version ${record.version}` : "Create reusable copy for lead communication activity."}</p>
          </div>
          <button type="button" className="icon-button modal-close" onClick={onClose} disabled={saving} aria-label="Close"><X size={20} /></button>
        </header>
        <div className="team-modal-body">
          {error && <div className="form-alert" role="alert">{error}</div>}
          <div className="form-grid">
            <Field label="Template Name" error={getFieldError("name")} required>
              <input value={form.name} maxLength={160} onChange={(event) => updateField("name", event.target.value)} autoFocus required />
            </Field>
            <Field label="Channel" error={getFieldError("channel")} required>
              <select value={form.channel} onChange={(event) => updateField("channel", event.target.value)} required>
                {templateChannels.map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
            </Field>
            <Field label="Category" error={getFieldError("category")} className="span-2" required>
              <input value={form.category} maxLength={80} onChange={(event) => updateField("category", event.target.value)} placeholder="Inquiry, Follow-up, Demo, Documents" required />
            </Field>
            <Field label="Template Body" error={getFieldError("body")} className="span-2" required>
              <textarea value={form.body} rows={9} maxLength={2000} onChange={(event) => updateField("body", event.target.value)} required />
            </Field>
          </div>
          <div className="template-helper">
            <span>Placeholders</span>
            <div>
              {templatePlaceholders.map((item) => (
                <button key={item} type="button" className="template-token" onClick={() => updateField("body", `${form.body}${form.body.endsWith(" ") || form.body.length === 0 ? "" : " "}{{${item}}}`)}>
                  {`{{${item}}}`}
                </button>
              ))}
            </div>
          </div>
          {isEditing && (
            <fieldset className="master-options status-option">
              <legend>Availability</legend>
              <label className="master-checkbox">
                <input type="checkbox" checked={form.isActive} onChange={(event) => updateField("isActive", event.target.checked)} />
                Active and available inside lead profiles
              </label>
            </fieldset>
          )}
        </div>
        <footer className="team-modal-actions">
          <button type="button" className="ghost-button" onClick={onClose} disabled={saving}>Cancel</button>
          <button type="submit" className="primary-button" disabled={saving}>{saving ? "Saving..." : isEditing ? "Save Changes" : "Add Template"}</button>
        </footer>
      </form>
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

function ChartTooltip({ active, payload, unit }) {
  if (!active || !payload?.length) {
    return null;
  }

  const item = payload[0];
  const name = item.name || item.payload?.name || "";
  const value = item.value ?? item.payload?.count ?? item.payload?.value ?? 0;

  return (
    <div className="chart-tooltip">
      <span>{item.payload?.name || name}</span>
      <strong>{formatNumber(value)} {unit}</strong>
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
      <label className="lead-search-field">
        <span>Search</span>
        <div className="lead-search-input">
          <Search size={16} />
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
        </div>
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

function LeadsTable({ leads, page, pageSize, total, loading, error, onRetry, onOpenLead, onPageChange, canSelect, selected, onToggleLead, onTogglePage }) {
  const totalPages = Math.max(1, Math.ceil(total / Math.max(pageSize, 1)));
  const start = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(total, page * pageSize);
  const selectedCount = leads.filter((lead) => selected[lead.id]).length;
  const selectAllRef = useRef(null);

  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = selectedCount > 0 && selectedCount < leads.length;
    }
  }, [selectedCount, leads.length]);

  return (
    <div className="table-card">
      {loading && <StatePanel title="Loading leads" message="Fetching live leads from CounselMate API..." />}
      {error && <StatePanel title="Could not load leads" message={error} action={onRetry} />}
      {!loading && !error && leads.length === 0 && <StatePanel title="No leads" message="No leads were found for this tenant." />}
      {!loading && !error && leads.length > 0 && (
      <table>
        <thead>
          <tr>
            {canSelect && <th className="selection-cell"><input ref={selectAllRef} type="checkbox" checked={leads.length > 0 && selectedCount === leads.length} onChange={onTogglePage} aria-label="Select all leads on this page" /></th>}
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
              {canSelect && <td className="selection-cell"><input type="checkbox" checked={Boolean(selected[lead.id])} onChange={() => onToggleLead(lead)} onClick={(event) => event.stopPropagation()} aria-label={`Select ${lead.studentName}`} /></td>}
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
        <div className="lead-pagination">
          <button className="pager" disabled={page <= 1 || loading} onClick={() => onPageChange(page - 1)}>Prev</button>
          <span className="pagination-status">Page {page} of {totalPages}</span>
          <button className="pager" disabled={page >= totalPages || loading} onClick={() => onPageChange(page + 1)}>Next</button>
        </div>
      </footer>
    </div>
  );
}

function FollowUpRow({ item, compact = false, saving = false, canManageLeads = false, onOpenLead, onComplete, onCancel, onReschedule }) {
  const scheduled = item.status === "Scheduled";
  const overdue = scheduled && new Date(item.dueAt).getTime() < Date.now();
  const dueLabel = item.status === "Completed"
    ? `Completed ${formatFollowUpLabel(item.completedAt)}`
    : item.status === "Cancelled"
      ? `Cancelled ${formatFollowUpLabel(item.cancelledAt)}`
      : formatFollowUpLabel(item.dueAt);

  return (
    <article className={`followup-row ${compact ? "compact" : ""}`}>
      <div className="channel-icon">{item.type[0]}</div>
      <div className="followup-main">
        <h3>{item.studentName}</h3>
        <div className="followup-badges">
          <Badge label={`${item.priority} Priority`} danger={["High", "Urgent"].includes(item.priority)} warning={item.priority === "Medium"} />
          <Badge label={item.status} muted={item.status !== "Scheduled"} />
          {overdue && <Badge label="Overdue" danger />}
        </div>
        <p>{item.leadId} - {item.assignedTo}</p>
      </div>
      <div className="followup-time" aria-label={dueLabel}>
        <small>{dueLabel}</small>
        <strong>{formatTime(item.dueAt)}</strong>
        <p>{formatDate(item.dueAt)}</p>
      </div>
      {!compact && (
        <div className="followup-actions">
          <button type="button" className="ghost-button" onClick={() => onOpenLead(item.leadId)} disabled={saving}>
            <Eye size={16} />
            Lead
          </button>
          {scheduled && canManageLeads && (
            <>
              <button type="button" className="ghost-button" onClick={() => onReschedule(item)} disabled={saving}>
                <CalendarDays size={16} />
                Reschedule
              </button>
              <button type="button" className="ghost-button danger-text" onClick={() => onCancel(item)} disabled={saving}>
                <X size={16} />
                Cancel
              </button>
              <button type="button" className="primary-button" onClick={() => onComplete(item)} disabled={saving}>
                <CheckCircle2 size={18} />
                {saving ? "Saving..." : "Complete"}
              </button>
            </>
          )}
        </div>
      )}
    </article>
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

function StatePanel({ title, message, action, actionLabel = "Retry" }) {
  return (
    <div className="state-panel">
      <strong>{title}</strong>
      <p>{message}</p>
      {action && <button className="soft-button" onClick={action}>{actionLabel}</button>}
    </div>
  );
}

function NotificationPanel({ data, status, onClose, onItemClick, onMarkAllRead, onRetry, onLoadMore }) {
  const hasMore = data.items.length < data.total;

  return (
    <section className="notification-panel" aria-label="Notifications">
      <header className="notification-panel-header">
        <div>
          <strong>Notifications</strong>
          <span>{data.unreadCount ? `${data.unreadCount} unread` : "You're up to date"}</span>
        </div>
        <button className="icon-button" onClick={onClose} aria-label="Close notifications">
          <X size={18} />
        </button>
      </header>
      {data.unreadCount > 0 && (
        <button className="notification-read-all" onClick={onMarkAllRead} disabled={status.saving}>
          <CheckCircle2 size={16} />
          {status.saving ? "Updating..." : "Mark all read"}
        </button>
      )}
      {status.error && (
        <div className="notification-error">
          <span>{status.error}</span>
          <button onClick={onRetry}>Retry</button>
        </div>
      )}
      <div className="notification-list">
        {status.loading && data.items.length === 0 && <p className="notification-empty">Loading notifications...</p>}
        {!status.loading && !status.error && data.items.length === 0 && (
          <p className="notification-empty">No reminders yet.</p>
        )}
        {data.items.map((item) => (
          <button
            key={item.id}
            className={`notification-item ${item.readAt ? "read" : "unread"}`}
            onClick={() => onItemClick(item)}
          >
            <span className={`notification-severity ${item.severity.toLowerCase()}`} />
            <span className="notification-copy">
              <strong>{item.title}</strong>
              <span>{item.message}</span>
              <small>{formatFollowUpLabel(item.createdAt)}</small>
            </span>
          </button>
        ))}
      </div>
      {hasMore && (
        <button className="notification-load-more" onClick={onLoadMore} disabled={status.loading}>
          {status.loading ? "Loading..." : "Load more"}
        </button>
      )}
    </section>
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

function formatFileSize(bytes) {
  if (!bytes) {
    return "0 KB";
  }

  if (bytes < 1024 * 1024) {
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function formatCurrency(value, currency = "INR") {
  const amount = Number(value || 0);
  return new Intl.NumberFormat("en-IN", {
    style: "currency",
    currency: currency || "INR",
    maximumFractionDigits: 2,
  }).format(amount);
}

function mappedPreviewValue(row, mapping, field) {
  const header = mapping?.[field];
  return header ? row.values?.[header] || "" : "";
}

function downloadFileResponse(response, fallbackFilename) {
  const url = URL.createObjectURL(response.blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = response.filename || fallbackFilename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function getFollowUpQueueCounts(followUps) {
  return followUps.reduce((counts, item) => {
    if (item.status === "Completed") {
      counts.completed += 1;
    } else if (item.status === "Cancelled") {
      counts.cancelled += 1;
    } else if (followUpMatchesQueue(item, "overdue")) {
      counts.overdue += 1;
    } else if (followUpMatchesQueue(item, "today")) {
      counts.today += 1;
    } else if (followUpMatchesQueue(item, "upcoming")) {
      counts.upcoming += 1;
    }
    return counts;
  }, { overdue: 0, today: 0, upcoming: 0, completed: 0, cancelled: 0 });
}

function followUpMatchesQueue(item, queue) {
  if (queue === "all") {
    return true;
  }
  if (queue === "completed") {
    return item.status === "Completed";
  }
  if (queue === "cancelled") {
    return item.status === "Cancelled";
  }
  if (item.status !== "Scheduled") {
    return false;
  }

  const dueAt = new Date(item.dueAt);
  if (Number.isNaN(dueAt.getTime())) {
    return false;
  }

  const now = new Date();
  const endOfToday = new Date(now);
  endOfToday.setHours(23, 59, 59, 999);

  if (queue === "overdue") {
    return dueAt < now;
  }
  if (queue === "today") {
    return dueAt >= now && dueAt <= endOfToday;
  }
  if (queue === "upcoming") {
    return dueAt > endOfToday;
  }

  return false;
}

function compareFollowUpsForQueue(left, right) {
  const leftTime = new Date(left.dueAt).getTime();
  const rightTime = new Date(right.dueAt).getTime();
  const safeLeft = Number.isNaN(leftTime) ? Number.MAX_SAFE_INTEGER : leftTime;
  const safeRight = Number.isNaN(rightTime) ? Number.MAX_SAFE_INTEGER : rightTime;
  return safeLeft - safeRight;
}

function isToday(value) {
  if (!value) {
    return false;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return false;
  }

  const today = new Date();
  return date.getFullYear() === today.getFullYear()
    && date.getMonth() === today.getMonth()
    && date.getDate() === today.getDate();
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
    defaultBranchId: null,
    defaultAssigneeUserId: null,
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

function defaultReportFilters() {
  const end = new Date();
  const start = new Date();
  start.setDate(end.getDate() - 29);
  return {
    startDate: toDateInputValue(start),
    endDate: toDateInputValue(end),
    branchId: "",
    courseId: "",
    sourceId: "",
    assignedUserId: "",
  };
}

function toDateInputValue(date) {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function createDefaultLeadForm(options, initialValues = {}) {
  return {
    studentName: "",
    guardianName: "",
    email: "",
    phone: "",
    city: "",
    courseId: options.courses[0]?.id || "",
    leadSourceId: options.sources[0]?.id || "",
    leadStageId: findOptionId(options.stages, "New Inquiry") || options.stages[0]?.id || "",
    branchId: options.branches.some((item) => item.id === options.defaultBranchId)
      ? options.defaultBranchId
      : options.branches[0]?.id || "",
    assignedUserId: options.counselors.some((item) => item.id === options.defaultAssigneeUserId)
      ? options.defaultAssigneeUserId
      : "",
    status: "New Lead",
    priority: "Medium",
    nextFollowUpAt: "",
    ...initialValues,
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

function areLeadProfileFormsEqual(left, right) {
  return [
    "studentName",
    "guardianName",
    "email",
    "phone",
    "city",
    "courseId",
    "leadSourceId",
    "leadStageId",
    "branchId",
    "assignedUserId",
    "status",
    "priority",
    "nextFollowUpAt",
  ].every((key) => (left?.[key] || "") === (right?.[key] || ""));
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
    dueAt: defaultFutureDateTimeLocal(),
  };
}

function createDefaultPaymentForm() {
  return {
    title: "",
    amountDue: "",
    dueDate: "",
    notes: "",
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

function validateCommunicationTemplateForm(form) {
  const errors = {};
  const name = form.name.trim();
  const category = form.category.trim();
  const body = form.body.trim();

  if (!name) errors.name = "Template name is required.";
  else if (name.length > 160) errors.name = "Template name must be 160 characters or fewer.";

  if (!templateChannels.includes(form.channel)) errors.channel = "Choose a supported channel.";

  if (!category) errors.category = "Category is required.";
  else if (category.length > 80) errors.category = "Category must be 80 characters or fewer.";

  if (!body) errors.body = "Template body is required.";
  else if (body.length > 2000) errors.body = "Template body must be 2000 characters or fewer.";

  const unknownPlaceholders = extractTemplateTokens(body).filter((item) => !templatePlaceholders.includes(item));
  if (unknownPlaceholders.length > 0) {
    errors.body = `Unsupported placeholder: {{${unknownPlaceholders[0]}}}.`;
  }

  return errors;
}

function extractTemplateTokens(value) {
  const tokens = [];
  const regex = /\{\{\s*([^}]+?)\s*\}\}/g;
  let match = regex.exec(value);
  while (match) {
    const token = match[1].trim();
    if (token && !tokens.includes(token)) {
      tokens.push(token);
    }
    match = regex.exec(value);
  }
  return tokens;
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

function defaultFutureDateTimeLocal() {
  const date = new Date();
  date.setMinutes(date.getMinutes() + 60);
  date.setSeconds(0, 0);
  if (date.getMinutes() > 0) {
    date.setHours(date.getHours() + 1, 0, 0, 0);
  }

  return toDateTimeLocalValue(date);
}

export default App;
