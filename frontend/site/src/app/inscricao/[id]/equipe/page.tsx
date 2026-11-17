import type { Metadata } from 'next';
import { notFound, redirect } from 'next/navigation';
import AssistenteDeEquipe from '@/components/inscricao/AssistenteDeEquipe';
import { obterInscricoesAbertas, obterTemporada } from '@/lib/api';

export const metadata: Metadata = { title: 'Inscrição de equipe' };

export default async function Pagina({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await obterTemporada(id);
  if (!t) notFound();
  // Só torneio aberto com inscrições abertas aceita equipe; nos demais, vale a inscrição individual.
  const aberta = (await obterInscricoesAbertas())?.find(x => x.temporadaId === id);
  if (t.publico !== 'Aberto' || !aberta) redirect(`/inscricao/${id}`);

  return (
    <main className="wrap">
      <AssistenteDeEquipe
        t={{
          temporadaId: id,
          campeonato: aberta.campeonato,
          ano: aberta.ano,
          fimInscricoes: aberta.fimInscricoes,
          taxa: aberta.taxaInscricao,
          categorias: aberta.categorias.map(c => ({ id: c.id, nome: c.nome, idadeMinima: c.idadeMinima, sexo: c.sexo as 'Masculino' | 'Feminino' | null })),
        }}
      />
    </main>
  );
}
