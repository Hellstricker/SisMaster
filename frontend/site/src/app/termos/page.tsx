import type { Metadata } from 'next';
import Link from 'next/link';
import { obterPagina } from '@/lib/api';
import { VERSAO_LEGAL, VIGENTE_DESDE, dadosLegais } from '@/lib/legal';

export const metadata: Metadata = { title: 'Termos de uso' };

export default async function Termos() {
  const d = dadosLegais(await obterPagina());
  return (
    <main className="wrap">
      <article className="texto-legal">
        <h1>Termos de uso</h1>
        <p className="sub">Versão {VERSAO_LEGAL} · em vigor desde {VIGENTE_DESDE}</p>

        <h2>1. Aceitação</h2>
        <p>
          Ao criar uma conta, inscrever-se ou usar o SisMaster você declara que leu e concorda com estes Termos e com a <Link href="/privacidade">Política de privacidade</Link>.
          Se não concordar, não use o sistema.
        </p>

        <h2>2. O que é o sistema</h2>
        <p>
          O SisMaster serve para a gestão dos campeonatos de basquete da {d.nome} (“Associação”): cadastro de atletas, inscrições, tabelas de jogos, súmulas, placar e classificações.
          A Associação pode alterar, suspender ou encerrar funcionalidades.
        </p>

        <h2>3. Conta e senha</h2>
        <ul>
          <li>Você é responsável por manter <b>sua senha em sigilo</b> e por tudo o que for feito com a sua conta. Não compartilhe o acesso.</li>
          <li>Use dados <b>verdadeiros e atualizados</b>. Informar dados falsos, ou de outra pessoa sem autorização, pode levar à recusa da inscrição e à desativação da conta.</li>
          <li>Contas de gestão e de arbitragem são criadas por administradores. Se perceber acesso indevido à sua conta, avise a Associação imediatamente.</li>
        </ul>

        <h2>4. Inscrições</h2>
        <ul>
          <li>A inscrição é uma <b>solicitação</b> e depende de <b>aprovação da diretoria</b>, que pode recusá-la com base no regulamento do campeonato (por exemplo, categoria, idade, vagas e pendências).</li>
          <li>A situação da inscrição é informada no sistema e por e-mail.</li>
          <li>Taxas, prazos, descontos e formas de pagamento seguem o regulamento da temporada. A devolução da taxa e a desistência seguem o mesmo regulamento; sem regra específica, a diretoria decide cada caso e informa o motivo.</li>
          <li>A inscrição de <b>menor de 18 anos</b> deve ser feita ou autorizada pelo responsável legal, que responde pelas informações prestadas.</li>
        </ul>

        <h2>5. Regulamento e arbitragem</h2>
        <p>
          A participação nos campeonatos segue o <b>regulamento do campeonato</b> e as regras oficiais de basquete (FIBA), com as adaptações da FIMBA e da FBBM para o basquete master.
          As decisões de arbitragem, a súmula oficial e a classificação publicada pela diretoria prevalecem sobre qualquer outro registro. Erros de súmula podem ser corrigidos
          pela diretoria conforme o regulamento.
        </p>

        <h2>6. Publicação de resultados e imagem</h2>
        <p>
          Ao participar, você está ciente de que <b>nome, número de camisa, estatísticas do jogo e resultados</b> são divulgados no placar, na súmula e na classificação, inclusive de forma pública.
          Dados de contato e documentos não são divulgados. O sistema não publica fotos nem vídeos; se a Associação divulgar imagens dos jogos, o uso da sua imagem
          depende da autorização que você der na inscrição, que é separada, específica e pode ser revogada.
        </p>

        <h2>7. Uso adequado</h2>
        <p>
          É proibido: tentar acessar contas ou dados de terceiros; burlar bloqueios ou a segurança do sistema; sobrecarregar o sistema; inserir dados falsos, ofensivos ou ilegais; usar o
          sistema para fins comerciais não autorizados.
        </p>

        <h2>8. Disponibilidade</h2>
        <p>
          Buscamos manter o sistema disponível, mas ele pode ficar indisponível por manutenção ou por falhas fora do nosso controle. Em dia de jogo, a súmula oficial pode ser
          preenchida em papel como contingência. A Associação não responde por perdas decorrentes de indisponibilidade temporária, dentro dos limites da lei.
        </p>

        <h2>9. Propriedade</h2>
        <p>
          O sistema, sua marca e seu conteúdo pertencem à Associação ou a seus licenciantes. Os dados esportivos (resultados e estatísticas) podem ser usados e divulgados pela Associação
          para fins de organização e promoção do esporte.
        </p>

        <h2>10. Encerramento da conta</h2>
        <p>
          Você pode pedir o encerramento da conta à Associação. A Associação pode <b>desativar</b> contas que violem estes Termos. O histórico esportivo é preservado conforme a
          <Link href="/privacidade"> Política de privacidade</Link> (item 8).
        </p>

        <h2>11. Alterações dos Termos</h2>
        <p>
          Os Termos podem ser alterados. Alterações relevantes serão avisadas no sistema e, se afetarem os seus direitos, será pedido novo aceite. O uso continuado depois do aviso, nos demais
          casos, indica concordância.
        </p>

        <h2>12. Lei aplicável e foro</h2>
        <p>
          Estes Termos seguem as leis brasileiras. {d.foro
            ? <>Fica eleito o foro da comarca de <b>{d.foro}</b>, salvo disposição legal em contrário.</>
            : <>Fica eleito o foro da comarca do domicílio do usuário, salvo disposição legal em contrário.</>}
        </p>
        <p><b>Contato:</b> {d.emailContato}</p>
        <p className="muted small">Última atualização: {VIGENTE_DESDE} · Versão {VERSAO_LEGAL}</p>
      </article>
    </main>
  );
}
