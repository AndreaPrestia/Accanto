import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

const NAV = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/users', label: 'Users' },
  { to: '/audit-logs', label: 'Audit logs' },
  { to: '/operations', label: 'Operations' },
  { to: '/system', label: 'System' }
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const onLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="flex h-full">
      <aside className="w-56 shrink-0 border-r border-accanto-200 bg-white">
        <div className="border-b border-accanto-200 px-4 py-4">
          <div className="text-sm font-semibold text-accanto-900">Accanto</div>
          <div className="text-xs uppercase tracking-wide text-accanto-500">Control Plane</div>
        </div>
        <nav className="p-2">
          {NAV.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 text-sm font-medium ${
                  isActive ? 'bg-accanto-100 text-accanto-900' : 'text-accanto-600 hover:bg-accanto-50'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between border-b border-accanto-200 bg-white px-6 py-3">
          <div className="text-sm text-accanto-500">Technical operations console</div>
          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-sm font-medium text-accanto-900">{user?.email}</div>
              <div className="text-xs text-accanto-500">{user?.roles.join(', ')}</div>
            </div>
            <button onClick={onLogout} className="btn-ghost">Logout</button>
          </div>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
