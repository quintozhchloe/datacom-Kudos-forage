import { useEffect, useMemo, useState } from 'react';
import { useAuth } from 'react-oidc-context';

const emptyForm = {
  toUserId: '',
  message: ''
};

const defaultFilters = {
  team: '',
  search: ''
};

const PAGE_SIZE = 8;

export default function App() {
  const auth = useAuth();
  const account = auth.user?.profile;
  const roles = account?.roles || account?.role || account?.['cognito:groups'] || [];
  const isAdmin = Array.isArray(roles)
    ? roles.some((role) => ['KudosAdmin', 'Admin'].includes(role))
    : ['KudosAdmin', 'Admin'].includes(roles);

  const [users, setUsers] = useState([]);
  const [kudos, setKudos] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [refreshToken, setRefreshToken] = useState(0);
  const [filters, setFilters] = useState(defaultFilters);
  const [form, setForm] = useState(emptyForm);
  const [status, setStatus] = useState({ type: 'idle', text: '' });
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [loadingKudos, setLoadingKudos] = useState(true);

  async function getAccessToken() {
    if (!auth.user) {
      return null;
    }

    return auth.user.access_token;
  }

  async function authorizedFetch(url, options = {}) {
    const token = await getAccessToken();
    if (!token) {
      throw new Error('Missing access token');
    }

    const headers = new Headers(options.headers || {});
    headers.set('Authorization', `Bearer ${token}`);

    return fetch(url, { ...options, headers });
  }

  useEffect(() => {
    let active = true;

    async function loadUsers() {
      if (!auth.isAuthenticated) {
        return;
      }

      try {
        setLoadingUsers(true);
        const res = await authorizedFetch('/api/users');
        if (!res.ok) {
          throw new Error('Failed to load users');
        }
        const data = await res.json();
        if (active) {
          setUsers(data);
        }
      } catch (error) {
        if (active) {
          setStatus({ type: 'error', text: 'Unable to load teammates.' });
        }
      } finally {
        if (active) {
          setLoadingUsers(false);
        }
      }
    }

    loadUsers();

    return () => {
      active = false;
    };
  }, [account]);

  useEffect(() => {
    let active = true;

    async function loadKudos() {
        if (!auth.isAuthenticated) {
          return;
        }

      try {
        setLoadingKudos(true);
        const params = new URLSearchParams();
        params.set('page', page.toString());
        params.set('pageSize', PAGE_SIZE.toString());

        if (filters.team) {
          params.set('team', filters.team);
        }

        if (filters.search.trim()) {
          params.set('search', filters.search.trim());
        }

        const res = await authorizedFetch(`/api/kudos?${params.toString()}`);
        if (!res.ok) {
          throw new Error('Failed to load kudos');
        }

        const data = await res.json();
        if (active) {
          setKudos(data.items);
          setTotal(data.total);
        }
      } catch (error) {
        if (active) {
          setStatus({ type: 'error', text: 'Unable to load kudos feed.' });
        }
      } finally {
        if (active) {
          setLoadingKudos(false);
        }
      }
    }

    loadKudos();

    return () => {
      active = false;
    };
  }, [account, page, filters.team, filters.search, refreshToken]);

  const selectedUser = useMemo(
    () => users.find((user) => user.id === form.toUserId),
    [users, form.toUserId]
  );

  const teams = useMemo(() => {
    const unique = new Set(users.map((user) => user.team));
    return Array.from(unique).sort();
  }, [users]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  async function handleSubmit(event) {
    event.preventDefault();
    setStatus({ type: 'idle', text: '' });

    if (!auth.isAuthenticated) {
      setStatus({ type: 'error', text: 'Please sign in before sending kudos.' });
      return;
    }

    if (!form.toUserId || !form.message.trim()) {
      setStatus({ type: 'error', text: 'Please choose a colleague and add a message.' });
      return;
    }

    try {
      const response = await authorizedFetch('/api/kudos', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          toUserId: form.toUserId,
          message: form.message.trim()
        })
      });

      if (!response.ok) {
        throw new Error('Failed to send kudos');
      }

      await response.json();
      setForm(emptyForm);
      setStatus({ type: 'success', text: `Kudos sent to ${selectedUser?.name || 'your teammate'}!` });
      setPage(1);
      setRefreshToken((prev) => prev + 1);
    } catch (error) {
      setStatus({ type: 'error', text: 'Could not send kudos. Try again.' });
    }
  }

  async function handleModeration(id, makeVisible) {
    const reason = window.prompt(
      makeVisible ? 'Optional reason for restoring visibility:' : 'Reason for hiding this kudos:'
    );

    try {
      const response = await authorizedFetch(`/api/kudos/${id}/visibility`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          isVisible: makeVisible,
          reason: reason || ''
        })
      });

      if (!response.ok) {
        throw new Error('Moderation failed');
      }

      setRefreshToken((prev) => prev + 1);
    } catch (error) {
      setStatus({ type: 'error', text: 'Could not update moderation status.' });
    }
  }

  async function handleDelete(id) {
    if (!window.confirm('Delete this kudos permanently?')) {
      return;
    }

    try {
      const response = await authorizedFetch(`/api/kudos/${id}`, {
        method: 'DELETE'
      });

      if (!response.ok) {
        throw new Error('Delete failed');
      }

      setRefreshToken((prev) => prev + 1);
    } catch (error) {
      setStatus({ type: 'error', text: 'Could not delete kudos.' });
    }
  }

  function handleTeamFilter(value) {
    setPage(1);
    setFilters((prev) => ({ ...prev, team: value }));
  }

  function handleSearch(value) {
    setPage(1);
    setFilters((prev) => ({ ...prev, search: value }));
  }

  function handleLogin() {
    auth.signinRedirect();
  }

  function handleLogout() {
    auth.signoutRedirect();
  }

  return (
    <div className="page">
      <header className="header">
        <div>
          <p className="eyebrow">Internal Recognition</p>
          <h1>Share Kudos</h1>
          <p className="subtitle">Send a quick note of appreciation and celebrate wins together.</p>
        </div>
        <div className="badge">Live Feed</div>
      </header>

      <section className="card auth-card">
        {auth.isAuthenticated ? (
          <div className="auth-row">
            <div>
              <h2>Signed in</h2>
              <p className="card-subtitle">
                {account?.name || account?.preferred_username}
                {isAdmin ? ' · Admin' : ''}
              </p>
            </div>
            <button type="button" className="ghost" onClick={handleLogout}>
              Sign out
            </button>
          </div>
        ) : (
          <div className="auth-row">
            <div>
              <h2>Sign in to continue</h2>
              <p className="card-subtitle">Use your company account to send kudos.</p>
            </div>
            <button type="button" onClick={handleLogin}>
              Sign in
            </button>
          </div>
        )}
      </section>

      {auth.isAuthenticated && (
        <>
          <section className="card form-card">
          <div className="form-header">
            <div>
              <h2>Send Kudos</h2>
              <p className="card-subtitle">Pick a teammate and write a short message.</p>
            </div>
          </div>

          <form onSubmit={handleSubmit} className="form">
            <label className="field">
              <span>Colleague</span>
              <select
                value={form.toUserId}
                onChange={(event) =>
                  setForm((prev) => ({ ...prev, toUserId: event.target.value }))
                }
                disabled={loadingUsers}
              >
                <option value="">Select a teammate</option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.name} · {user.team}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span>Message</span>
              <textarea
                rows="4"
                maxLength={240}
                placeholder={
                  selectedUser
                    ? `What did ${selectedUser.name} do that deserves recognition?`
                    : 'Share a quick note of appreciation.'
                }
                value={form.message}
                onChange={(event) => setForm((prev) => ({ ...prev, message: event.target.value }))}
              />
              <span className="helper">{form.message.length}/240</span>
            </label>

            <button type="submit">Send Kudos</button>
          </form>

          {status.text && (
            <div className={`status ${status.type}`}>{status.text}</div>
          )}
          </section>

          <section className="card feed-card">
          <div className="feed-header">
            <div>
              <h2>Recent Kudos</h2>
              <p className="card-subtitle">{total} total recognition moments.</p>
            </div>
            <span className="pill">Page {page} of {totalPages}</span>
          </div>

          <div className="filters">
            <label className="field inline">
              <span>Team</span>
              <select
                value={filters.team}
                onChange={(event) => handleTeamFilter(event.target.value)}
              >
                <option value="">All teams</option>
                {teams.map((team) => (
                  <option key={team} value={team}>
                    {team}
                  </option>
                ))}
              </select>
            </label>
            <label className="field inline">
              <span>Search</span>
              <input
                type="search"
                placeholder="Search messages or names"
                value={filters.search}
                onChange={(event) => handleSearch(event.target.value)}
              />
            </label>
          </div>

          {loadingKudos ? (
            <p className="muted">Loading the latest kudos...</p>
          ) : kudos.length === 0 ? (
            <p className="muted">No kudos yet. Be the first to recognize someone.</p>
          ) : (
            <div className="feed">
              {kudos.map((item) => (
                <article key={item.id} className={`kudos ${item.isVisible ? '' : 'hidden'}`}>
                  <div>
                    <p className="kudos-title">
                      <span>{item.toUserName}</span>
                      <span className="kudos-team">{item.toUserTeam}</span>
                    </p>
                    <p className="kudos-message">“{item.message}”</p>
                    <p className="kudos-meta">
                      From {item.fromUserName} · {new Date(item.createdAt).toLocaleString()}
                    </p>
                    {item.isVisible === false && (
                      <p className="kudos-flag">Hidden by moderation</p>
                    )}
                  </div>
                  {isAdmin && (
                    <div className="kudos-actions">
                      <button
                        type="button"
                        className="ghost"
                        onClick={() => handleModeration(item.id, !item.isVisible)}
                      >
                        {item.isVisible ? 'Hide' : 'Show'}
                      </button>
                      <button
                        type="button"
                        className="ghost danger"
                        onClick={() => handleDelete(item.id)}
                      >
                        Delete
                      </button>
                    </div>
                  )}
                </article>
              ))}
            </div>
          )}

          <div className="pagination">
            <button
              type="button"
              className="ghost"
              onClick={() => setPage((prev) => Math.max(1, prev - 1))}
              disabled={page <= 1 || loadingKudos}
            >
              Previous
            </button>
            <button
              type="button"
              className="ghost"
              onClick={() => setPage((prev) => Math.min(totalPages, prev + 1))}
              disabled={page >= totalPages || loadingKudos}
            >
              Next
            </button>
          </div>
          </section>
        </>
      )}
    </div>
  );
}
