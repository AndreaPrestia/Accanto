import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export default function AppShell() {
  const { user, logout } = useAuth();
  const loc = useLocation();
  const showCircleNav = /^\/care-circles\/[^/]+/.test(loc.pathname) && !loc.pathname.endsWith('/new');

  return (
    <div className="min-h-full flex flex-col">
      <header className="sticky top-0 bg-white border-b border-accanto-100 z-10">
        <div className="max-w-2xl mx-auto px-4 py-3 flex items-center justify-between">
          <Link to="/" className="font-semibold text-accanto-700">Accanto</Link>
          {user && (
            <div className="flex items-center gap-3 text-sm">
              <Link to="/account" className="text-accanto-700 hover:underline hidden sm:inline">{user.displayName}</Link>
              <Link to="/account" className="text-accanto-700 hover:underline sm:hidden">Account</Link>
              <button onClick={logout} className="text-accanto-700 hover:underline">Esci</button>
            </div>
          )}
        </div>
      </header>

      <main className="flex-1">
        <div className="max-w-2xl mx-auto px-4 py-4 pb-24">
          <Outlet />
        </div>
      </main>

      {showCircleNav && <CircleBottomNav />}
    </div>
  );
}

function CircleBottomNav() {
  const match = useLocation().pathname.match(/^\/care-circles\/([^/]+)/);
  if (!match) return null;
  const id = match[1];
  const items: { to: string; label: string }[] = [
    { to: `/care-circles/${id}`, label: 'Cerchio' },
    { to: `/care-circles/${id}/timeline`, label: 'Diario' },
    { to: `/care-circles/${id}/documents`, label: 'Documenti' },
    { to: `/care-circles/${id}/questions`, label: 'Domande' },
    { to: `/care-circles/${id}/shared-updates`, label: 'Aggiornamenti' }
  ];
  return (
    <nav className="fixed bottom-0 inset-x-0 bg-white border-t border-accanto-100">
      <div className="max-w-2xl mx-auto grid grid-cols-5 text-xs">
        {items.map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            end={it.to === `/care-circles/${id}`}
            className={({ isActive }) =>
              `py-3 text-center ${isActive ? 'text-accanto-900 font-semibold' : 'text-accanto-500'}`
            }
          >
            {it.label}
          </NavLink>
        ))}
      </div>
    </nav>
  );
}
