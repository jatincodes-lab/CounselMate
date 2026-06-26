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
  Menu,
  MoreVertical,
  Plus,
  Search,
  Settings,
  Users,
} from "lucide-react";
import { useMemo, useState } from "react";
import { activities, counselors, followUps, leads, stages } from "./data/mockData";

const navItems = [
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
  const activeLabel = navItems.find((item) => item.id === activePage)?.label || "Dashboard";

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
          {navItems.map((item) => {
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

        <button className="sidebar-action">
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
                <strong>Rahul Sharma</strong>
                <span>Senior Counsellor</span>
              </div>
              <div className="avatar">RS</div>
            </div>
          </div>
        </header>

        <section className="content">
          {activePage === "dashboard" && <Dashboard />}
          {activePage === "leads" && <LeadsPage />}
          {activePage === "pipeline" && <PipelinePage />}
          {activePage === "followups" && <FollowUpsPage />}
          {activePage === "counselors" && <CounselorsPage />}
          {activePage === "reports" && <ReportsPage />}
          {activePage === "settings" && <SettingsPage />}
        </section>
      </main>
    </div>
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

function Dashboard() {
  return (
    <>
      <PageTitle
        title="Counsellor Dashboard"
        subtitle="Overview of lead flow, admissions, follow-ups, and counsellor productivity."
        action={
          <button className="primary-button">
            <Plus size={18} />
            New Lead
          </button>
        }
      />

      <div className="metric-grid">
        <Metric title="Total Leads" value="1,284" trend="+12%" />
        <Metric title="New Leads" value="42" trend="+4%" warning />
        <Metric title="Contacted" value="856" trend="+18%" />
        <Metric title="Enrolled" value="219" trend="+7%" />
      </div>

      <div className="dashboard-grid">
        <Card title="Conversion Pipeline" className="wide-card">
          <div className="bar-chart">
            {[34, 51, 72, 59, 80, 42].map((height, index) => (
              <span key={index} style={{ height: `${height}%` }} />
            ))}
          </div>
        </Card>

        <Card title="Today's Schedule" badge="5 Pending">
          <div className="schedule-list">
            {followUps.map((item) => (
              <FollowUpRow key={item.student} item={item} compact />
            ))}
          </div>
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

function LeadsPage() {
  return (
    <>
      <PageTitle
        title="Leads Management"
        subtitle="Review and manage student admission pipelines."
        action={
          <button className="primary-button">
            <Plus size={18} />
            Add Lead
          </button>
        }
      />
      <FilterBar />
      <LeadsTable />
    </>
  );
}

function PipelinePage() {
  const grouped = useMemo(
    () =>
      stages.map((stage) => ({
        ...stage,
        leads: leads.filter((lead) => lead.stage === stage.name || (stage.name === "Contacted" && lead.status === "Follow Up")),
      })),
    []
  );

  return (
    <>
      <PageTitle
        title="Lead Pipeline"
        subtitle="Manage and track student enrollment progress."
        action={
          <button className="primary-button">
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
      <div className="kanban">
        {grouped.map((stage) => (
          <section className="kanban-column" key={stage.name}>
            <header>
              <h3>{stage.name}</h3>
              <span>{stage.count}</span>
            </header>
            {stage.leads.map((lead) => (
              <article className="lead-card" key={lead.id}>
                <div className="lead-card-top">
                  <Badge label={lead.course.split(" ")[0]} />
                  <MoreVertical size={18} />
                </div>
                <h4>{lead.name}</h4>
                <p>{lead.course}</p>
                <footer>
                  <span className={lead.priority === "High" ? "danger-text" : ""}>{lead.nextFollowUp}</span>
                  <span className="mini-avatar">{initials(lead.name)}</span>
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
    </>
  );
}

function FollowUpsPage() {
  return (
    <>
      <PageTitle title="Follow-ups" subtitle="Manage your daily student engagement pipeline." />
      <div className="two-column">
        <div>
          <div className="tabs">
            <button className="active">Today <span>8</span></button>
            <button>Upcoming</button>
            <button>Overdue</button>
          </div>
          <div className="followup-list">
            {followUps.map((item) => (
              <FollowUpRow key={item.student} item={item} />
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

function SettingsPage() {
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
              {stages.map((stage) => (
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

function LeadsTable() {
  return (
    <div className="table-card">
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
            <tr key={lead.id}>
              <td><input type="checkbox" /></td>
              <td>
                <div className="student-cell">
                  <span>{initials(lead.name)}</span>
                  <div>
                    <strong>{lead.name}</strong>
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
      <footer className="table-footer">
        <span>Showing 5 of 124 leads</span>
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
        <h3>{item.student}</h3>
        <Badge label={`${item.priority} Priority`} danger={item.priority === "High"} warning={item.priority === "Medium"} />
        <p>{item.course}</p>
      </div>
      <div>
        <small>Scheduled</small>
        <strong>{item.time}</strong>
        <p>{item.due}</p>
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
  return name.split(" ").map((part) => part[0]).join("").slice(0, 2).toUpperCase();
}

export default App;
