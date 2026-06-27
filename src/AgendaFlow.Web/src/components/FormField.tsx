import { type InputHTMLAttributes } from 'react';

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  id: string;
}

export default function FormField({ label, error, id, ...inputProps }: FormFieldProps) {
  return (
    <div style={{ marginBottom: '0.875rem' }}>
      <label htmlFor={id} style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, marginBottom: '0.25rem' }}>
        {label}
      </label>
      <input
        id={id}
        aria-invalid={!!error}
        aria-describedby={error ? `${id}-error` : undefined}
        style={{
          width: '100%',
          padding: '0.5rem 0.625rem',
          borderRadius: 6,
          border: `1px solid ${error ? 'var(--color-error)' : 'var(--color-border)'}`,
          fontSize: '0.875rem',
          background: 'var(--color-bg)',
          color: 'var(--color-text)',
        }}
        {...inputProps}
      />
      {error && <span id={`${id}-error`} role="alert" style={{ fontSize: '0.75rem', color: 'var(--color-error)', marginTop: '0.25rem', display: 'block' }}>{error}</span>}
    </div>
  );
}
