import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { servicesApi, type ServiceDto, ApiError } from '../../api/client';
import AppShell from '../../components/AppShell';
import Modal from '../../components/Modal';
import FormField from '../../components/FormField';

const schema = z.object({
  name: z.string().min(1, 'Nome é obrigatório'),
  description: z.string().optional(),
  durationMinutes: z.coerce.number().int().min(1, 'Mínimo 1 minuto').max(480),
  price: z.coerce.number().min(0, 'Preço não pode ser negativo'),
  bufferBeforeMinutes: z.coerce.number().int().min(0).default(0),
  bufferAfterMinutes: z.coerce.number().int().min(0).default(0),
});

type FormData = z.infer<typeof schema>;

export default function ServicesPage() {
  const qc = useQueryClient();
  const [modal, setModal] = useState<'create' | ServiceDto | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  const { data: services, isLoading, error } = useQuery({
    queryKey: ['services'],
    queryFn: () => servicesApi.list(),
  });

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const openCreate = () => { reset({}); setServerError(null); setModal('create'); };
  const openEdit = (s: ServiceDto) => {
    reset({
      name: s.name,
      description: s.description ?? '',
      durationMinutes: s.durationMinutes,
      price: s.price,
      bufferBeforeMinutes: s.bufferBeforeMinutes,
      bufferAfterMinutes: s.bufferAfterMinutes,
    });
    setServerError(null);
    setModal(s);
  };

  const toggleMutation = useMutation({
    mutationFn: (s: ServiceDto) => s.isActive ? servicesApi.deactivate(s.id) : servicesApi.activate(s.id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['services'] }),
  });

  const onSubmit = async (data: FormData) => {
    setServerError(null);
    try {
      const payload = { ...data, currency: 'BRL', description: data.description || undefined };
      if (modal === 'create') {
        await servicesApi.create(payload);
      } else if (modal) {
        await servicesApi.update((modal as ServiceDto).id, payload);
      }
      await qc.invalidateQueries({ queryKey: ['services'] });
      setModal(null);
    } catch (err) {
      setServerError(err instanceof ApiError ? (err.detail ?? err.message) : 'Erro ao salvar serviço.');
    }
  };

  return (
    <AppShell>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
        <h1 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Serviços</h1>
        <button onClick={openCreate} style={btnPrimary}>+ Novo serviço</button>
      </div>

      {isLoading && <p style={{ color: 'var(--color-text-muted)' }}>Carregando...</p>}
      {error && <p style={{ color: 'var(--color-error)' }}>Erro ao carregar serviços.</p>}

      {services && services.length === 0 && (
        <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>Nenhum serviço cadastrado.</p>
      )}

      {services && services.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
          {services.map(s => (
            <div key={s.id} style={{
              background: 'var(--color-surface)',
              border: '1px solid var(--color-border)',
              borderRadius: 8,
              padding: '0.875rem 1rem',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              gap: '1rem',
            }}>
              <div style={{ flex: 1 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <span style={{ fontWeight: 500, fontSize: '0.9375rem' }}>{s.name}</span>
                  {!s.isActive && <span style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', background: 'var(--color-border)', borderRadius: 4, padding: '0.125rem 0.375rem' }}>Inativo</span>}
                </div>
                {s.description && <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.125rem' }}>{s.description}</p>}
                <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>
                  {s.durationMinutes} min · {s.price.toLocaleString('pt-BR', { style: 'currency', currency: s.currency })}
                  {(s.bufferBeforeMinutes > 0 || s.bufferAfterMinutes > 0) && ` · buffer ${s.bufferBeforeMinutes}/${s.bufferAfterMinutes} min`}
                </p>
              </div>
              <div style={{ display: 'flex', gap: '0.5rem', flexShrink: 0 }}>
                <button onClick={() => openEdit(s)} style={btnSecondary}>Editar</button>
                <button
                  onClick={() => toggleMutation.mutate(s)}
                  disabled={toggleMutation.isPending}
                  style={{ ...btnSecondary, color: s.isActive ? 'var(--color-error)' : 'var(--color-success)' }}
                >
                  {s.isActive ? 'Desativar' : 'Ativar'}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {modal !== null && (
        <Modal title={modal === 'create' ? 'Novo serviço' : 'Editar serviço'} onClose={() => setModal(null)}>
          <form onSubmit={handleSubmit(onSubmit)} noValidate>
            <FormField id="name" label="Nome" error={errors.name?.message} {...register('name')} />
            <FormField id="description" label="Descrição (opcional)" error={errors.description?.message} {...register('description')} />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
              <FormField id="durationMinutes" label="Duração (min)" type="number" min="1" max="480" error={errors.durationMinutes?.message} {...register('durationMinutes')} />
              <FormField id="price" label="Preço (R$)" type="number" min="0" step="0.01" error={errors.price?.message} {...register('price')} />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
              <FormField id="bufferBeforeMinutes" label="Buffer antes (min)" type="number" min="0" error={errors.bufferBeforeMinutes?.message} {...register('bufferBeforeMinutes')} />
              <FormField id="bufferAfterMinutes" label="Buffer depois (min)" type="number" min="0" error={errors.bufferAfterMinutes?.message} {...register('bufferAfterMinutes')} />
            </div>
            {serverError && <p role="alert" style={{ color: 'var(--color-error)', fontSize: '0.8125rem', marginBottom: '0.75rem' }}>{serverError}</p>}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
              <button type="button" onClick={() => setModal(null)} style={btnSecondary}>Cancelar</button>
              <button type="submit" disabled={isSubmitting} style={btnPrimary}>{isSubmitting ? 'Salvando...' : 'Salvar'}</button>
            </div>
          </form>
        </Modal>
      )}
    </AppShell>
  );
}

const btnPrimary: React.CSSProperties = {
  padding: '0.5rem 1rem',
  background: 'var(--color-primary)',
  color: 'white',
  border: 'none',
  borderRadius: 6,
  fontSize: '0.875rem',
  cursor: 'pointer',
  fontWeight: 500,
};

const btnSecondary: React.CSSProperties = {
  padding: '0.375rem 0.75rem',
  background: 'none',
  color: 'var(--color-text)',
  border: '1px solid var(--color-border)',
  borderRadius: 6,
  fontSize: '0.8125rem',
  cursor: 'pointer',
};
