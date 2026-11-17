import Link from 'next/link';
import { type TemporadaAberta } from '@/lib/api';
import { brl } from '@/lib/detalhe';
import { dataCurta } from '@/lib/inscricao';
import { ATLETAS_PARA_RODIZIO, MAXIMO_DE_ATLETAS, MINIMO_DE_ATLETAS } from '@/lib/equipe';

/** O primeiro passo de um torneio aberto: participar como responsável de uma equipe ou como atleta ainda sem equipe. */
export default function ComoParticipar({ t }: { t: TemporadaAberta }) {
  return (
    <div className="como-participar">
      <h1>{t.campeonato} {t.ano}</h1>
      <p className="muted">
        Inscrições até <b>{dataCurta(t.fimInscricoes)}</b>
        {t.taxaInscricao != null && <> · taxa de <b>{brl(t.taxaInscricao)} por atleta</b></>}
        {' '}· torneio <b>aberto</b>: participar <b>não torna ninguém associado</b>.
      </p>

      <h2>Como você quer participar?</h2>
      <div className="opcoes">
        <Link className="opcao" href={`/inscricao/${t.temporadaId}/equipe`} data-testid="opcao-equipe">
          <span className="ico" aria-hidden="true">🏀</span>
          <h3>Sou responsável por uma equipe</h3>
          <ul>
            <li>Você monta o elenco (de {MINIMO_DE_ATLETAS} a {MAXIMO_DE_ATLETAS} atletas) e cadastra cada atleta.</li>
            <li>Cada atleta recebe um convite por e-mail e confirma, ele mesmo, a participação e as autorizações.</li>
            <li>Você acompanha quem já confirmou e fala com a diretoria.</li>
          </ul>
          <span className="btn primary">Inscrever uma equipe</span>
        </Link>
        <Link className="opcao" href={`/inscricao/${t.temporadaId}?modo=individual`} data-testid="opcao-individual">
          <span className="ico" aria-hidden="true">🙋</span>
          <h3>Quero jogar, ainda sem equipe</h3>
          <ul>
            <li>Você faz a sua inscrição individual, escolhendo a categoria.</li>
            <li>Depois de aprovada e paga a taxa, a diretoria coloca você em uma equipe.</li>
            <li>Você acompanha tudo em “Minhas inscrições”.</li>
          </ul>
          <span className="btn primary">Inscrever-me como atleta</span>
        </Link>
      </div>

      <p className="aviso info">
        <b>Foi convidado por uma equipe?</b> Abra o link do convite que chegou por e-mail: nele você confirma os seus dados e dá as autorizações. Não precisa fazer outra inscrição.
      </p>
      <p className="small muted">
        Já se inscreveu? Veja em <Link href="/minhas-inscricoes">Minhas inscrições</Link>. Atletas menores de 18 anos são inscritos pela diretoria, com a autorização do responsável legal:{' '}
        <Link href="/contato">fale com a diretoria</Link>. Uma equipe com {ATLETAS_PARA_RODIZIO} ou mais atletas permite o rodízio.
      </p>
    </div>
  );
}
