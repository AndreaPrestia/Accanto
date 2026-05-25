import { Routes, Route, Navigate } from 'react-router-dom';
import AppShell from './components/AppShell';
import { RequireAuth } from './auth/RequireAuth';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import DashboardPage from './pages/DashboardPage';
import NewCareCirclePage from './pages/NewCareCirclePage';
import CareCirclePage from './pages/CareCirclePage';
import TimelinePage from './pages/TimelinePage';
import DocumentsPage from './pages/DocumentsPage';
import DoctorQuestionsPage from './pages/DoctorQuestionsPage';
import SharedUpdatesPage from './pages/SharedUpdatesPage';
import DifficultDayPage from './pages/DifficultDayPage';
import InviteAcceptPage from './pages/InviteAcceptPage';
import AccountPage from './pages/AccountPage';
import AuditPage from './pages/AuditPage';

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/" element={<RequireAuth><DashboardPage /></RequireAuth>} />
        <Route path="/care-circles/new" element={<RequireAuth><NewCareCirclePage /></RequireAuth>} />
        <Route path="/care-circles/:id" element={<RequireAuth><CareCirclePage /></RequireAuth>} />
        <Route path="/care-circles/:id/timeline" element={<RequireAuth><TimelinePage /></RequireAuth>} />
        <Route path="/care-circles/:id/documents" element={<RequireAuth><DocumentsPage /></RequireAuth>} />
        <Route path="/care-circles/:id/questions" element={<RequireAuth><DoctorQuestionsPage /></RequireAuth>} />
        <Route path="/care-circles/:id/shared-updates" element={<RequireAuth><SharedUpdatesPage /></RequireAuth>} />
        <Route path="/care-circles/:id/difficult-day" element={<RequireAuth><DifficultDayPage /></RequireAuth>} />
        <Route path="/care-circles/:id/audit" element={<RequireAuth><AuditPage /></RequireAuth>} />
        <Route path="/invite/:token" element={<InviteAcceptPage />} />
        <Route path="/account" element={<RequireAuth><AccountPage /></RequireAuth>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
