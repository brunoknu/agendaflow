import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { professionalsApi, servicesApi, type ProfessionalDto, ApiError } from '../../api/client';
import AppShell from '../../components/AppShell';
import Modal from '../../components/Modal';
import FormField from '../../components/FormField';

const schema = z.object({
  name: z.string().min(1, 'Nome é obrigatório'),
  email: z.string().email('E-mail inválido').optional().or(z.literal('')),
  phone: z.string().optional(),
});

type FormData = z.infer<typeof schema>;

const btnPrimary: React.CSSProperties = {
  padding: '0.5rem 1rem', background: 'var(--color-primary)', color: 'white',
  border: 'none', borderRadius: 6, fontSize: '0.875rem', cursor: 'pointer', fontWeight: 500,
};

const btnSecondary: React.CSSProperties = {
  padding: '0.375rem 0.75rem', background: 'none', color: 'var(--color-text)',
  border: '1px solid var(--color-border)', borderRadius: 6, fontSize: '0.8125rem', cursor: 'pointer',
};

export default function ProfessionalsPage() {
  const qc = useQueryClient();
  const [modal, setModal] = useState<'create' | ProfessionalDto | null>(null);
  const [servicesModal, setServicesModal] = useState<ProfessionalDto | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  const { data: professionals, isLoading, error } = useQuery({
    queryKey: ['professionals'],
    queryFn: () => professionalsApi.list(),
  });

  const { data: allServices } = useQuery({
    queryKey: ['services-active'],
    queryFn: () => servicesApi.list(true),
    enabled: !!servicesModal,
  });

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const openCreate = () => { reset({}); setServerError(null); setModal('create'); };
  const openEdit = (p: ProfessionalDto) => {
    reset({ name: p.name, email: p.email ?? '', phone: p.phone ?? '' });
    setServerError(null);
    setModal(p);
  };

  const toggleMutation = useMutation({
    mutationFn: (p: ProfessionalDto) =>
      p.isActive ? professionalsApi.deactivate(p.id) : professionalsApi.activate(p.id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['professionals'] }),
  });

  const linkServiceMutation = useMutation({
    mutationFn: ({ professionalId, serviceId }: { professionalId: string; serviceId: string }) =>
      professionalsApi.linkService(professionalId, serviceId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['professionals'] }),
  });

  const unlinkServiceMutation = useMutation({
    mutationFn: ({ professionalId, serviceId }: { professionalId: string; serviceId: string }) =>
      professionalsApi.unlinkService(professionalId, serviceId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['professionals'] }),
  });

  const onSubmit = async (data: FormData) => {
    setServerError(null);
    try {
      const payload = { name: data.name, email: data.email || undefined, phone: data.phone || undefined };
      if (modal === 'create') {
        await professionalsApi.create(payload);
      } else if (modal) {
        await professionalsApi.update((modal as ProfessionalDto).id, payload);
      }
      await qc.invalidateQueries({ queryKey: ['professionals'] });
      setModal(null);
    } catch (err) {
      setServerError(err instanceof ApiError ? (err.detail ?? err.message) : 'Erro ao salvar.');
    }
  };

  return (
    <AppShell>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
        <h1 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Profissionais</h1>
        <button onClick={openCreate} style={btnPrimary}>+ Novo profissional</button>
      </div>

      {isLoading && <p style={{ color: 'var(--color-text-muted)' }}>Carregando...</p>}
      {error && <p style={{ color: 'var(--color-error)' }}>Erro ao carregar profissionais.</p>}

      {professionals && professionals.length === 0 && (
        <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>Nenhum profissional cadastrado.</p>
      )}

      {professionals && professionals.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
          {professionals.map(p => (
            <div key={p.id} style={{
              background: 'var(--color-surface)', border: '1px solid var(--color-border)',
              borderRadius: 8, padding: '0.875rem 1rem',
              display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '1rem',
            }}>
              <div style={{ flex: 1 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <span style={{ fontWeight: 500, fontSize: '0.9375rem' }}>{p.name}</span>
                  {!p.isActive && (
                    <span style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', background: 'var(--color-border)', borderRadius: 4, padding: '0.125rem 0.375rem' }}>
                      Inativo
                    </span>
                  )}
                </div>
                <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.125rem' }}>
                  {[p.email, p.phone].filter(Boolean).join(' · ') || 'Sem contato'}
                  {p.serviceIds && p.serviceIds.length > 0 && ` · ${p.serviceIds.length} serviço(s)`}
                </p>
              </div>
              <div style={{ display: 'flex', gap: '0.5rem', flexShrink: 0 }}>
                <button onClick={() => setServicesModal(p)} style={btnSecondary}>Serviços</button>
                <button onClick={() => openEdit(p)} style={btnSecondary}>Editar</button>
                <button
                  onClick={() => toggleMutation.mutate(p)}
                  disabled={toggleMutation.isPending}
                  style={{ ...btnSecondary, color: p.isActive ? 'var(--color-error)' : 'var(--color-success)' }}
                >
                  {p.isActive ? 'Desativar' : 'Ativar'}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Create / Edit modal */}
      {modal !== null && (
        <Modal title={modal === 'create' ? 'Novo profissional' : 'Editar profissional'} onClose={() => setModal(null)}>
          <form onSubmit={handleSubmit(onSubmit)} noValidate>
            <FormField id="name" label="Nome" error={errors.name?.message} {...register('name')} />
            <FormField id="email" label="E-mail (opcional)" type="email" error={errors.email?.message} {...register('email')} />
            <FormField id="phone" label="Telefone (opcional)" type="tel" error={errors.phone?.message} {...register('phone')} />
            {serverError && <p role="alert" style={{ color: 'var(--color-error)', fontSize: '0.8125rem', marginBottom: '0.75rem' }}>{serverError}</p>}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
              <button type="button" onClick={() => setModal(null)} style={btnSecondary}>Cancelar</button>
              <button type="submit" disabled={isSubmitting} style={btnPrimary}>{isSubmitting ? 'Salvando...' : 'Salvar'}</button>
            </div>
          </form>
        </Modal>
      )}

      {/* Services link modal */}
      {servicesModal && (() => {
        const currentP = professionals?.find(p => p.id === servicesModal.id);
        const linkedIds = new Set(currentP?.serviceIds ?? []);
        return (
          <Modal title={`Serviços — ${servicesModal.name}`} onClose={() => setServicesModal(null)}>
            {!allServices && <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>Carregando...</p>}
            {allServices && allServices.length === 0 && (
              <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>Nenhum serviço ativo cadastrado.</p>
            )}
            {allServices && allServices.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                {allServices.map(s => {
                  const isLinked = linkedIds.has(s.id);
                  return (
                    <div key={s.id} style={{
                      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                      padding: '0.5rem 0.75rem',
                      border: `1px solid ${isLinked ? 'var(--color-primary)' : 'var(--color-border)'}`,
                      borderRadius: 6,
                      background: isLinked ? 'color-mix(in srgb, var(--color-primary) 5%, transparent)' : 'none',
                    }}>
                      <div>
                        <span style={{ fontSize: '0.875rem', fontWeight: isLinked ? 500 : 400 }}>{s.name}</span>
                        <span style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginLeft: '0.5rem' }}>
                          {s.durationMinutes} min
                        </span>
                      </div>
                      {isLinked ? (
                        <button
                          onClick={() => unlinkServiceMutation.mutate({ professionalId: servicesModal.id, serviceId: s.id })}
                          disabled={unlinkServiceMutation.isPending || linkServiceMutation.isPending}
                          style={{ ...btnSecondary, color: 'var(--color-error)' }}
                        >
                          Remover
                        </button>
                      ) : (
                        <button
                          onClick={() => linkServiceMutation.mutate({ professionalId: servicesModal.id, serviceId: s.id })}
                          disabled={linkServiceMutation.isPending || unlinkServiceMutation.isPending}
                          style={{ ...btnSecondary, color: 'var(--color-success)' }}
                        >
                          + Vincular
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </Modal>
        );
      })()}
    </AppShell>
  );
}
