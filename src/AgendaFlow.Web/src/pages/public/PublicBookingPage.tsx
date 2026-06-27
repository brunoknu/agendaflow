import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { publicApi, ApiError } from '../../api/client';

type Step = 'service' | 'professional' | 'datetime' | 'customer' | 'review' | 'done' | 'error';

function formatDate(date: Date) {
  return date.toLocaleDateString('pt-BR', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
}

function formatTime(utcStr: string) {
  return new Date(utcStr).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

function getToday(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export default function PublicBookingPage() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const [step, setStep] = useState<Step>('service');
  const [selectedService, setSelectedService] = useState<{ id: string; name: string; durationMinutes: number } | null>(null);
  const [selectedProfessional, setSelectedProfessional] = useState<{ id: string; name: string } | null>(null);
  const [selectedDate, setSelectedDate] = useState(getToday());
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);
  const [customer, setCustomer] = useState({ name: '', email: '', phone: '' });
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const { data: services, isLoading: loadingServices } = useQuery({
    queryKey: ['public-services', tenantSlug],
    queryFn: () => publicApi.getServices(tenantSlug!),
    enabled: !!tenantSlug,
  });

  const { data: professionals, isLoading: loadingProfessionals } = useQuery({
    queryKey: ['public-professionals', tenantSlug, selectedService?.id],
    queryFn: () => publicApi.getProfessionals(tenantSlug!, selectedService?.id),
    enabled: step === 'professional' && !!selectedService,
  });

  const { data: slotsData, isLoading: loadingSlots } = useQuery({
    queryKey: ['public-slots', tenantSlug, selectedProfessional?.id, selectedService?.id, selectedDate],
    queryFn: () => publicApi.getAvailableSlots(
      tenantSlug!, selectedProfessional!.id, selectedService!.id, selectedDate
    ),
    enabled: step === 'datetime' && !!selectedProfessional && !!selectedService,
  });

  const handleBook = async () => {
    if (!selectedSlot || !selectedService || !selectedProfessional || !tenantSlug) return;
    setSubmitting(true);
    try {
      await publicApi.book(tenantSlug, {
        professionalId: selectedProfessional.id,
        serviceId: selectedService.id,
        startAtUtc: selectedSlot,
        customerName: customer.name,
        customerEmail: customer.email,
        customerPhone: customer.phone || undefined,
        idempotencyKey: `${customer.email}-${selectedSlot}-${selectedService.id}`,
      });
      setStep('done');
    } catch (err) {
      if (err instanceof ApiError) setErrorMsg(err.detail ?? err.message);
      else setErrorMsg('Erro ao finalizar agendamento.');
      setStep('error');
    } finally {
      setSubmitting(false);
    }
  };

  if (!tenantSlug) return <div style={{ padding: '2rem' }}>Página não encontrada.</div>;

  return (
    <div style={{ maxWidth: 600, margin: '0 auto', padding: '2rem' }}>
      <h1 style={{ fontSize: '1.5rem', marginBottom: '1.5rem', color: 'var(--color-primary)' }}>
        Agendar
      </h1>

      {/* Step: Choose service */}
      {step === 'service' && (
        <section>
          <h2 style={{ fontSize: '1rem', marginBottom: '1rem' }}>Escolha o serviço</h2>
          {loadingServices && <p>Carregando...</p>}
          {services?.length === 0 && <p style={{ color: 'var(--color-text-muted)' }}>Nenhum serviço disponível.</p>}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {services?.map(s => (
              <button
                key={s.id}
                onClick={() => { setSelectedService(s); setStep('professional'); }}
                style={{
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                  padding: '0.75rem 1rem', border: '1px solid var(--color-border)',
                  borderRadius: 6, background: 'var(--color-surface)', cursor: 'pointer',
                  textAlign: 'left',
                }}
              >
                <div>
                  <strong>{s.name}</strong>
                  {s.description && <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>{s.description}</p>}
                </div>
                <div style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)', whiteSpace: 'nowrap' }}>
                  {s.durationMinutes} min · {s.price.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                </div>
              </button>
            ))}
          </div>
        </section>
      )}

      {/* Step: Choose professional */}
      {step === 'professional' && (
        <section>
          <button onClick={() => setStep('service')} style={{ marginBottom: '1rem', cursor: 'pointer', color: 'var(--color-primary)', background: 'none', border: 'none' }}>← Voltar</button>
          <h2 style={{ fontSize: '1rem', marginBottom: '1rem' }}>Escolha o profissional</h2>
          {loadingProfessionals && <p>Carregando...</p>}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {professionals?.map(p => (
              <button
                key={p.id}
                onClick={() => { setSelectedProfessional(p); setStep('datetime'); }}
                style={{
                  padding: '0.75rem 1rem', border: '1px solid var(--color-border)',
                  borderRadius: 6, background: 'var(--color-surface)', cursor: 'pointer', textAlign: 'left',
                }}
              >
                {p.name}
              </button>
            ))}
          </div>
        </section>
      )}

      {/* Step: Choose date/time */}
      {step === 'datetime' && (
        <section>
          <button onClick={() => setStep('professional')} style={{ marginBottom: '1rem', cursor: 'pointer', color: 'var(--color-primary)', background: 'none', border: 'none' }}>← Voltar</button>
          <h2 style={{ fontSize: '1rem', marginBottom: '1rem' }}>Escolha a data e horário</h2>
          <div style={{ marginBottom: '1rem' }}>
            <input
              type="date"
              value={selectedDate}
              min={getToday()}
              onChange={e => { setSelectedDate(e.target.value); setSelectedSlot(null); }}
              style={{ padding: '0.5rem', borderRadius: 6, border: '1px solid var(--color-border)' }}
            />
          </div>
          {loadingSlots && <p>Verificando horários...</p>}
          {slotsData?.slots.length === 0 && (
            <p style={{ color: 'var(--color-text-muted)' }}>Nenhum horário disponível nesta data.</p>
          )}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
            {slotsData?.slots.map(slot => (
              <button
                key={slot}
                onClick={() => { setSelectedSlot(slot); setStep('customer'); }}
                style={{
                  padding: '0.5rem 0.75rem',
                  border: `1px solid ${selectedSlot === slot ? 'var(--color-primary)' : 'var(--color-border)'}`,
                  borderRadius: 6,
                  background: selectedSlot === slot ? 'var(--color-primary)' : 'var(--color-surface)',
                  color: selectedSlot === slot ? 'white' : 'var(--color-text)',
                  cursor: 'pointer',
                }}
              >
                {formatTime(slot)}
              </button>
            ))}
          </div>
        </section>
      )}

      {/* Step: Customer info */}
      {step === 'customer' && (
        <section>
          <button onClick={() => setStep('datetime')} style={{ marginBottom: '1rem', cursor: 'pointer', color: 'var(--color-primary)', background: 'none', border: 'none' }}>← Voltar</button>
          <h2 style={{ fontSize: '1rem', marginBottom: '1rem' }}>Seus dados</h2>
          {[
            { label: 'Nome', key: 'name', type: 'text', required: true },
            { label: 'E-mail', key: 'email', type: 'email', required: true },
            { label: 'Telefone (opcional)', key: 'phone', type: 'tel', required: false },
          ].map(f => (
            <div key={f.key} style={{ marginBottom: '0.75rem' }}>
              <label style={{ display: 'block', fontSize: '0.875rem', marginBottom: '0.25rem' }}>{f.label}</label>
              <input
                type={f.type}
                required={f.required}
                value={customer[f.key as keyof typeof customer]}
                onChange={e => setCustomer(c => ({ ...c, [f.key]: e.target.value }))}
                style={{ width: '100%', padding: '0.5rem', borderRadius: 6, border: '1px solid var(--color-border)' }}
              />
            </div>
          ))}
          <button
            onClick={() => setStep('review')}
            disabled={!customer.name || !customer.email}
            style={{
              width: '100%', padding: '0.625rem', background: 'var(--color-primary)',
              color: 'white', border: 'none', borderRadius: 6, cursor: 'pointer',
              opacity: (!customer.name || !customer.email) ? 0.5 : 1,
            }}
          >
            Revisar
          </button>
        </section>
      )}

      {/* Step: Review */}
      {step === 'review' && selectedSlot && selectedService && selectedProfessional && (
        <section>
          <button onClick={() => setStep('customer')} style={{ marginBottom: '1rem', cursor: 'pointer', color: 'var(--color-primary)', background: 'none', border: 'none' }}>← Voltar</button>
          <h2 style={{ fontSize: '1rem', marginBottom: '1rem' }}>Confirmar agendamento</h2>
          <div style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 8, padding: '1rem', marginBottom: '1rem' }}>
            {[
              ['Serviço', selectedService.name],
              ['Profissional', selectedProfessional.name],
              ['Data', formatDate(new Date(selectedSlot))],
              ['Horário', formatTime(selectedSlot)],
              ['Nome', customer.name],
              ['E-mail', customer.email],
              customer.phone ? ['Telefone', customer.phone] : null,
            ].filter(Boolean).map(([label, value]) => (
              <div key={label} style={{ display: 'flex', justifyContent: 'space-between', padding: '0.375rem 0', borderBottom: '1px solid var(--color-border)' }}>
                <span style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>{label}</span>
                <span style={{ fontSize: '0.875rem' }}>{value}</span>
              </div>
            ))}
          </div>
          <p style={{ fontSize: '0.8rem', color: 'var(--color-text-muted)', marginBottom: '1rem' }}>
            Você receberá um e-mail para confirmar o agendamento.
          </p>
          <button
            onClick={handleBook}
            disabled={submitting}
            style={{
              width: '100%', padding: '0.625rem', background: 'var(--color-primary)',
              color: 'white', border: 'none', borderRadius: 6, cursor: 'pointer',
            }}
          >
            {submitting ? 'Enviando...' : 'Solicitar agendamento'}
          </button>
        </section>
      )}

      {/* Step: Done */}
      {step === 'done' && (
        <section style={{ textAlign: 'center', padding: '2rem 0' }}>
          <h2 style={{ color: 'var(--color-success)', marginBottom: '0.5rem' }}>Solicitação enviada!</h2>
          <p style={{ color: 'var(--color-text-muted)' }}>
            Verifique seu e-mail ({customer.email}) para confirmar o agendamento.
          </p>
        </section>
      )}

      {/* Step: Error */}
      {step === 'error' && (
        <section style={{ textAlign: 'center', padding: '2rem 0' }}>
          <h2 style={{ color: 'var(--color-error)', marginBottom: '0.5rem' }}>Não foi possível agendar</h2>
          <p style={{ color: 'var(--color-text-muted)', marginBottom: '1rem' }}>{errorMsg}</p>
          <button onClick={() => setStep('datetime')} style={{ cursor: 'pointer', color: 'var(--color-primary)', background: 'none', border: 'none' }}>
            Tentar outro horário
          </button>
        </section>
      )}
    </div>
  );
}
