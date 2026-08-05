import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
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
  const location = useLocation();
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => { setDrawerOpen(false); }, [location.pathname]);

  const onLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="relative flex h-full">
      {drawerOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/40 md:hidden"
          onClick={() => setDrawerOpen(false)}
          aria-hidden="true"
        />
      )}

      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-56 shrink-0 flex-col border-r border-accanto-200 bg-white transition-transform md:static md:translate-x-0 ${
          drawerOpen ? 'translate-x-0' : '-translate-x-full md:translate-x-0'
        }`}
      >
        <div className="border-b border-accanto-200 px-4 py-4">
          <div className="text-sm font-semibold text-accanto-900">Accanto</div>
          <div className="text-xs uppercase tracking-wide text-accanto-500">Control Plane</div>
        </div>
        <nav className="flex-1 p-2">
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
        <header className="flex items-center justify-between gap-2 border-b border-accanto-200 bg-white px-3 py-3 md:px-6">
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setDrawerOpen(true)}
              className="btn-ghost !px-2 !py-1 md:hidden"
              aria-label="Open menu"
            >
              ☰
            </button>
            <div className="hidden text-sm text-accanto-500 sm:block">
              Technical operations console
            </div>
          </div>
          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <div className="text-sm font-medium text-accanto-900">{user?.email}</div>
              <div className="text-xs text-accanto-500">{user?.roles.join(', ')}</div>
            </div>
            <button onClick={onLogout} className="btn-ghost">Logout</button>
          </div>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto p-4 md:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
