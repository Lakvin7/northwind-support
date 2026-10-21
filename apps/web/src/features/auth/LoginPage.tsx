import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Field } from '../../components/Field';
import { Spinner } from '../../components/Spinner';

interface LoginForm {
  email: string;
  password: string;
}

const emptyForm: LoginForm = { email: '', password: '' };
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function LoginPage() {
  const { agent, isInitializing, login } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [form, setForm] = useState<LoginForm>(emptyForm);
  const [errors, setErrors] = useState<Partial<Record<keyof LoginForm, string>>>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const returnTo = getReturnTo(location.state);

  if (isInitializing) {
    return (
      <main className="app__main">
        <Spinner label="Checking session" />
      </main>
    );
  }

  if (agent) {
    return <Navigate to={returnTo} replace />;
  }

  const update = (patch: Partial<LoginForm>) => {
    setForm((current) => ({ ...current, ...patch }));
    setSubmitError(undefined);
  };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0 || isSubmitting) {
      return;
    }

    setIsSubmitting(true);

    try {
      await login(form.email.trim(), form.password);
      await navigate(returnTo, { replace: true });
    } catch (caught) {
      setSubmitError(toErrorMessage(caught, 'Unable to sign in.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="app">
      <header className="app__header">
        <span className="app__brand">Northwind Support</span>
      </header>

      <main className="app__main">
        <section>
          <header className="page-header">
            <div>
              <h1>Sign in</h1>
              <p className="page-header__subtitle">Use your support agent account.</p>
            </div>
          </header>

          {submitError && <ErrorMessage message={submitError} />}

          <form className="card form" onSubmit={(event) => void submit(event)} noValidate>
            <Field id="login-email" label="Email" error={errors.email}>
              {(fieldProps) => (
                <input
                  {...fieldProps}
                  name="email"
                  type="email"
                  autoComplete="email"
                  value={form.email}
                  disabled={isSubmitting}
                  onChange={(event) => {
                    update({ email: event.target.value });
                    setErrors((current) => ({ ...current, email: undefined }));
                  }}
                />
              )}
            </Field>

            <Field id="login-password" label="Password" error={errors.password}>
              {(fieldProps) => (
                <input
                  {...fieldProps}
                  name="password"
                  type="password"
                  autoComplete="current-password"
                  value={form.password}
                  disabled={isSubmitting}
                  onChange={(event) => {
                    update({ password: event.target.value });
                    setErrors((current) => ({ ...current, password: undefined }));
                  }}
                />
              )}
            </Field>

            <button type="submit" className="button button--primary" disabled={isSubmitting}>
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </button>
          </form>
        </section>
      </main>
    </div>
  );
}

function validate(form: LoginForm): Partial<Record<keyof LoginForm, string>> {
  const errors: Partial<Record<keyof LoginForm, string>> = {};
  const email = form.email.trim();

  if (email.length === 0) {
    errors.email = 'Enter your email address.';
  } else if (!emailPattern.test(email)) {
    errors.email = 'Enter a valid email address.';
  }

  if (form.password.length === 0) {
    errors.password = 'Enter your password.';
  }

  return errors;
}

function getReturnTo(state: unknown): string {
  if (typeof state !== 'object' || state === null) {
    return '/tickets';
  }

  const returnTo = (state as Record<string, unknown>).returnTo;
  if (
    typeof returnTo !== 'string' ||
    !returnTo.startsWith('/') ||
    returnTo.startsWith('//') ||
    returnTo.startsWith('/login')
  ) {
    return '/tickets';
  }

  return returnTo;
}
