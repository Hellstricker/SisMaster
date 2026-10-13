import type { Metadata } from 'next';
import HistoricoLegal from '@/components/HistoricoLegal';
import TextoLegal from '@/components/TextoLegal';
import { obterLegal, obterPagina } from '@/lib/api';
import { dadosLegais, dataLegal, preencherTexto } from '@/lib/legal';

export const metadata: Metadata = { title: 'Política de privacidade' };

export default async function Privacidade() {
  const [pagina, legal] = await Promise.all([obterPagina(), obterLegal()]);
  const d = dadosLegais(pagina);
  const proprio = legal.textoPrivacidade;
  return (
    <main className="wrap">
      <article className="texto-legal">
        <h1>Política de privacidade</h1>
        <p className="sub">Versão {legal.versao} · em vigor desde {dataLegal(legal.vigenteDesde)}</p>
        {proprio ? <TextoLegal texto={preencherTexto(proprio, d)} /> : (
        <>

        <h2>1. Quem somos</h2>
        <p>
          O <b>SisMaster</b> é o sistema de gestão de campeonatos de basquete da <b>{d.nome}</b> (“Associação”), CNPJ <b>{d.cnpj}</b>, com sede em <b>{d.endereco}</b>.
          A Associação é a <b>controladora</b> dos dados pessoais tratados no sistema, nos termos da Lei Geral de Proteção de Dados (Lei nº 13.709/2018, “LGPD”).
        </p>
        <p><b>Encarregado pelo tratamento de dados:</b> {d.encarregado} — <b>{d.emailPrivacidade}</b>.</p>

        <h2>2. Quais dados coletamos</h2>
        <div className="rolagem">
          <table>
            <thead><tr><th className="l">Grupo</th><th className="l">Dados</th><th className="l">De onde vêm</th></tr></thead>
            <tbody>
              <tr><td className="l"><b>Identificação</b></td><td className="l">nome completo, CPF, data de nascimento, sexo</td><td className="l">informados por você (ou pelo responsável) no cadastro ou na inscrição, ou cadastrados pela diretoria</td></tr>
              <tr><td className="l"><b>Contato</b></td><td className="l">e-mail e telefone</td><td className="l">idem</td></tr>
              <tr><td className="l"><b>Participação</b></td><td className="l">inscrições em campeonatos e temporadas, categoria, equipe, situação da inscrição (recebida, aprovada, recusada) e do pagamento da taxa</td><td className="l">geradas ao se inscrever e pela gestão do campeonato</td></tr>
              <tr><td className="l"><b>Jogos</b></td><td className="l">presença em súmulas, número de camisa, pontos, faltas e demais estatísticas, e sanções</td><td className="l">registradas pela arbitragem e pela mesa durante os jogos</td></tr>
              <tr><td className="l"><b>Conta de acesso</b></td><td className="l">e-mail de login, senha (guardada <b>somente de forma criptografada e irreversível</b>), perfil de acesso, vínculo com a associação, tentativas de entrada</td><td className="l">criada por você ou por um administrador</td></tr>
              <tr><td className="l"><b>Dados técnicos</b></td><td className="l">endereço IP e registros de funcionamento do servidor</td><td className="l">gerados automaticamente ao usar o sistema</td></tr>
            </tbody>
          </table>
        </div>
        <p>
          Não coletamos dados bancários nem de cartão de crédito no sistema e não coletamos dados pessoais sensíveis (como saúde, religião ou biometria).
          Se a Associação passar a exigir documento adicional, como atestado médico ou foto, esta política será atualizada antes.
        </p>

        <h2>3. Para que usamos os dados e em que base legal</h2>
        <div className="rolagem">
          <table>
            <thead><tr><th className="l">Finalidade</th><th className="l">Base legal (LGPD, art. 7º)</th></tr></thead>
            <tbody>
              <tr><td className="l">Cadastrar e identificar você de forma única (o CPF evita duplicidades e homônimos)</td><td className="l">execução de contrato ou de procedimentos preliminares, a seu pedido (inc. V)</td></tr>
              <tr><td className="l">Processar a inscrição, aprová-la ou recusá-la e definir categoria e equipe</td><td className="l">execução de contrato / procedimentos preliminares (inc. V)</td></tr>
              <tr><td className="l">Organizar tabelas, súmulas, resultados e classificação dos campeonatos</td><td className="l">execução de contrato (inc. V) e legítimo interesse da Associação na organização esportiva (inc. IX)</td></tr>
              <tr><td className="l">Verificar a idade para enquadrar nas categorias</td><td className="l">execução de contrato (inc. V)</td></tr>
              <tr><td className="l">Cobrar e conciliar a taxa de inscrição</td><td className="l">execução de contrato (inc. V)</td></tr>
              <tr><td className="l">Enviar e-mails do sistema: confirmação de e-mail, recuperação de senha e mudança de situação da inscrição</td><td className="l">execução de contrato (inc. V)</td></tr>
              <tr><td className="l">Divulgar nome, camisa, estatísticas e resultados dos jogos (item 4) e uso de imagem, quando você autorizar na inscrição</td><td className="l">legítimo interesse (inc. IX) e, quando houver autorização específica, consentimento (inc. I)</td></tr>
              <tr><td className="l">Proteger o sistema contra acesso indevido e fraude</td><td className="l">legítimo interesse (inc. IX) e prevenção à fraude</td></tr>
              <tr><td className="l">Cumprir obrigações legais e atender a autoridades</td><td className="l">cumprimento de obrigação legal (inc. II)</td></tr>
            </tbody>
          </table>
        </div>
        <p><b>Não usamos seus dados para publicidade de terceiros nem os vendemos.</b></p>

        <h2>4. Quem vê os seus dados</h2>
        <ul>
          <li><b>Equipe da Associação:</b> os administradores veem os cadastros e as inscrições. O administrador de uma associação vê apenas os dados daquela associação.</li>
          <li><b>Árbitros e mesários:</b> veem apenas o necessário para o jogo (nomes, números de camisa, súmula) e só das associações a que estão vinculados.</li>
          <li><b>Público em geral:</b> o placar e a súmula de cada jogo divulgam <b>nome, número de camisa e estatísticas do jogo</b> dos atletas, e a classificação divulga as equipes. <b>CPF, data de nascimento, e-mail e telefone nunca aparecem em páginas públicas.</b></li>
        </ul>

        <h2>5. Crianças e adolescentes</h2>
        <p>
          Há categorias de base e o sistema guarda a data de nascimento para enquadrá-las. O tratamento de dados de menores de 18 anos é feito <b>no melhor interesse do menor</b> (LGPD, art. 14),
          com <b>consentimento específico e em destaque de pelo menos um dos pais ou do responsável legal</b>. A inscrição de menor deve ser feita ou autorizada pelo responsável, que declara
          ciência desta política e responde pelas informações prestadas.
        </p>

        <h2>6. Com quem compartilhamos (operadores)</h2>
        <p>Contratamos prestadores que tratam dados em nosso nome e só seguem as nossas instruções:</p>
        <div className="rolagem">
          <table>
            <thead><tr><th className="l">Prestador</th><th className="l">Para quê</th><th className="l">Dados</th></tr></thead>
            <tbody>
              <tr><td className="l">Provedor de envio de e-mail (SMTP)</td><td className="l">enviar os e-mails do sistema</td><td className="l">e-mail do destinatário e o conteúdo da mensagem</td></tr>
              <tr><td className="l">Provedor de hospedagem do servidor</td><td className="l">guardar o sistema e o banco de dados, com cópias de segurança</td><td className="l">todos os dados do sistema</td></tr>
            </tbody>
          </table>
        </div>
        <p>
          Hoje o sistema <b>não cobra taxas pela internet</b>: o pagamento é registrado manualmente pela diretoria, e nenhum dado de cartão ou conta bancária passa pelo sistema.
          Se isso mudar, esta política será atualizada antes. Podemos compartilhar dados com autoridades quando a lei exigir.
        </p>

        <h2>7. Cookies e armazenamento no navegador</h2>
        <p>
          O sistema <b>não usa cookies de publicidade nem de rastreamento</b>. Guarda no seu navegador apenas a <b>sessão de acesso</b> (para você continuar logado) e a preferência de tema claro ou escuro.
          Esses dados saem quando você sai do sistema ou quando a sessão vence.
        </p>

        <h2>8. Por quanto tempo guardamos</h2>
        <div className="rolagem">
          <table>
            <thead><tr><th className="l">Dado</th><th className="l">Prazo</th></tr></thead>
            <tbody>
              <tr><td className="l">Cadastro e histórico esportivo (inscrições, súmulas, estatísticas, classificações)</td><td className="l">enquanto houver interesse esportivo e histórico dos campeonatos; você pode pedir a eliminação ou a anonimização do que não for necessário preservar (item 9)</td></tr>
              <tr><td className="l">Dados de cobrança</td><td className="l">5 anos, pelo prazo fiscal</td></tr>
              <tr><td className="l">Conta de acesso</td><td className="l">enquanto a conta existir; contas desativadas ficam preservadas para manter o histórico</td></tr>
              <tr><td className="l">Registros técnicos do servidor (inclui o endereço IP)</td><td className="l">6 meses</td></tr>
            </tbody>
          </table>
        </div>
        <p>Depois do prazo, os dados são eliminados ou anonimizados, salvo se a lei exigir a guarda.</p>

        <h2>9. Seus direitos</h2>
        <p>Você pode, a qualquer momento, pedir à Associação (LGPD, art. 18):</p>
        <ul>
          <li>confirmar que tratamos dados seus e <b>acessá-los</b>;</li>
          <li><b>corrigir</b> dados incompletos, inexatos ou desatualizados;</li>
          <li><b>anonimizar, bloquear ou eliminar</b> dados desnecessários ou tratados fora da lei;</li>
          <li>pedir a <b>portabilidade</b> dos dados;</li>
          <li>saber com quem compartilhamos os dados;</li>
          <li><b>revogar o consentimento</b>, quando ele for a base do tratamento;</li>
          <li>se opor a um tratamento feito com base no legítimo interesse.</li>
        </ul>
        <p>
          Peça pelo e-mail <b>{d.emailPrivacidade}</b>. Respondemos em até <b>15 dias</b>. Alguns dados do histórico esportivo (por exemplo, o resultado de um jogo já realizado) podem precisar ser
          mantidos para preservar a integridade das competições; nesses casos explicamos o motivo. Você também pode reclamar à <b>Autoridade Nacional de Proteção de Dados (ANPD)</b>.
        </p>

        <h2>10. Como protegemos os dados</h2>
        <p>
          Senhas guardadas de forma criptografada e irreversível; acesso por perfil, com cada administrador restrito à sua associação; sessão com tempo limite; bloqueio da conta após tentativas
          erradas de senha; comunicação protegida por HTTPS; cópias de segurança do banco de dados. Nenhum sistema é totalmente imune a falhas. Em caso de incidente que possa causar risco ou dano
          relevante, comunicaremos você e a ANPD, conforme a lei.
        </p>

        <h2>11. Mudanças nesta política</h2>
        <p>
          Podemos atualizar esta política. A versão em vigor mostra a data e o número da última alteração. Mudanças relevantes serão avisadas no sistema ou por e-mail.
        </p>
        </>
        )}
        <HistoricoLegal legal={legal} />
      </article>
    </main>
  );
}
