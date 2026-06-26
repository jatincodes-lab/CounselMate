export const leads = [
  {
    id: "LD-1001",
    name: "Arjun Adhikari",
    email: "arjun.a@email.com",
    phone: "+91 98765 43210",
    source: "Google Ads",
    course: "MBA Global",
    counselor: "S. Verma",
    status: "Enrolled",
    priority: "High",
    stage: "Enrolled",
    nextFollowUp: "Today, 4:30 PM",
  },
  {
    id: "LD-1002",
    name: "Priya Sharma",
    email: "priya.s@outlook.com",
    phone: "+91 91234 56789",
    source: "Website",
    course: "Data Science",
    counselor: "R. Khanna",
    status: "Interested",
    priority: "Medium",
    stage: "Interested",
    nextFollowUp: "Tomorrow, 11:00 AM",
  },
  {
    id: "LD-1003",
    name: "Michael Jones",
    email: "m.jones@gmail.com",
    phone: "+91 99887 76655",
    source: "LinkedIn",
    course: "UI/UX Design",
    counselor: "Rahul Sharma",
    status: "Follow Up",
    priority: "High",
    stage: "Demo Scheduled",
    nextFollowUp: "Today, 2:45 PM",
  },
  {
    id: "LD-1004",
    name: "Deepak Reddy",
    email: "d.reddy@tcs.com",
    phone: "+91 90000 11223",
    source: "Referral",
    course: "Full Stack Dev",
    counselor: "S. Verma",
    status: "Dropped",
    priority: "Low",
    stage: "Dropped",
    nextFollowUp: "No follow-up",
  },
  {
    id: "LD-1005",
    name: "Kriti Luthra",
    email: "k.luthra@gmail.com",
    phone: "+91 88776 65544",
    source: "Offline Expo",
    course: "Digital Marketing",
    counselor: "Rahul Sharma",
    status: "New Lead",
    priority: "Medium",
    stage: "New Inquiry",
    nextFollowUp: "Today, 6:00 PM",
  },
];

export const followUps = [
  {
    student: "Aarav Mehta",
    course: "Computer Science, UK",
    type: "Call",
    time: "10:30 AM",
    priority: "High",
    due: "In 45m",
  },
  {
    student: "Ishani Kapoor",
    course: "MBA, Canada",
    type: "WhatsApp",
    time: "12:15 PM",
    priority: "Medium",
    due: "Scheduled",
  },
  {
    student: "Vikram Malhotra",
    course: "Visa document follow-up",
    type: "Email",
    time: "02:45 PM",
    priority: "Low",
    due: "Scheduled",
  },
];

export const activities = [
  "Arjun Sharma completed enrollment payment for BBA 2024.",
  "Lisa Wong sent a new inquiry about Scholarship Program.",
  "Missed follow-up call with David Miller.",
];

export const stages = [
  { name: "New Inquiry", count: 12 },
  { name: "Contacted", count: 8 },
  { name: "Demo Scheduled", count: 5 },
  { name: "Application Started", count: 4 },
  { name: "Enrolled", count: 3 },
];

export const counselors = [
  { name: "Rahul Sharma", role: "Senior Counselor", leads: 142, conversion: "24%" },
  { name: "S. Verma", role: "Counselor", leads: 128, conversion: "21%" },
  { name: "R. Khanna", role: "Counselor", leads: 96, conversion: "18%" },
];
