import { useMemo, useState } from 'react';
import type { Sexo } from '../../api/categorias';
import { enviarInscricao } from '../../api/inscricoes';
import type { TemporadaCategoria } from '../../api/temporadas';
import DataInput, { dataValida, ddmmParaIso } from '../../components/DataInput/DataInput';
import { aplicarMascaraCpf, aplicarMascaraTelefone, formatarMoeda } from '../../utils/formatacao';
import Modal from './Modal';
import styles from './Inscricoes.module.css';

interface Props {
  temporadaId: string;
  anoTemporada: number;
  categorias: TemporadaCategoria[];
  categoriasIniciais: string[];
  onFechar: () => void;
  onEnviado: () => void;
}

const POSICOES = ['Armador', 'Ala Armador', 'Ala', 'Ala Pivô', 'Pivô'];

export default function ModalNovaInscricao({ temporadaId, anoTemporada, categorias, categoriasIniciais, onFechar, onEnviado }: Props) {
  const [form, setForm] = useState({
    nome: '', cpf: '', nascimento: '', sexo: 'Masculino' as Sexo, email: '', telefone: '',
    alturaCm: '', pesoKg: '', posicao: '', possuiPlano: false, nomePlano: '', lgpd: false,
  });
  const [selecionadas, setSelecionadas] = useState<string[]>(categoriasIniciais);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function campo<K extends keyof typeof form>(k: K, v: (typeof form)[K]) {
    setForm(p => ({ ...p, [k]: v }));
    setErro('');
  }

  // Avisos de "fora do esperado" (a diretoria decide na aprovação; o candidato não justifica)
  const avisos = useMemo(() => {
    if (!dataValida(form.nascimento, 1900, anoTemporada)) return [];
    const idade = anoTemporada - Number(form.nascimento.slice(6));
    return categorias
      .filter(c => selecionadas.includes(c.id))
      .flatMap(c => {
        const a: string[] = [];
        if (!c.aceitaAbaixoIdadeMinima && idade < c.idadeMinima) a.push(`${c.nome}: ${idade} anos (mínimo ${c.idadeMinima})`);
        if (c.sexo && c.sexo !== form.sexo) a.push(`${c.nome}: categoria ${c.sexo.toLowerCase()}`);
        return a;
      });
  }, [form.nascimento, form.sexo, selecionadas, categorias, anoTemporada]);

  function alternar(id: string) {
    setSelecionadas(p => (p.includes(id) ? p.filter(x => x !== id) : [...p, id]));
    setErro('');
  }

  async function enviar() {
    if (form.nome.trim().length < 3) { setErro('Informe o nome completo.'); return; }
    if (form.cpf.replace(/\D/g, '').length !== 11) { setErro('CPF deve ter 11 dígitos.'); return; }
    if (!dataValida(form.nascimento, 1900, anoTemporada)) { setErro('Informe a data de nascimento (DD/MM/AAAA).'); return; }
    if (!form.email.includes('@')) { setErro('Informe um e-mail válido.'); return; }
    const digitosTel = form.telefone.replace(/\D/g, '');
    if (digitosTel && digitosTel.length < 10) { setErro('Telefone incompleto: informe o DDD e o número.'); return; }
    if (selecionadas.length === 0) { setErro('Escolha ao menos uma categoria.'); return; }
    if (form.possuiPlano && !form.nomePlano.trim()) { setErro('Informe o nome do plano de saúde.'); return; }
    if (!form.lgpd) { setErro('O consentimento LGPD é obrigatório.'); return; }

    setSalvando(true);
    try {
      await enviarInscricao({
        temporadaId,
        nome: form.nome.trim(),
        cpf: form.cpf,
        nascimento: ddmmParaIso(form.nascimento),
        sexo: form.sexo,
        email: form.email.trim(),
        telefone: form.telefone.replace(/\D/g, '') || undefined,
        alturaCm: form.alturaCm ? Number(form.alturaCm) : undefined,
        pesoKg: form.pesoKg ? Number(form.pesoKg) : undefined,
        posicao: form.posicao || undefined,
        possuiPlanoSaude: form.possuiPlano,
        nomePlanoSaude: form.possuiPlano ? form.nomePlano.trim() : undefined,
        consentimentoLgpd: form.lgpd,
        temporadaCategoriaIds: selecionadas,
      });
      onEnviado();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao enviar inscrição.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Nova inscrição" largo erro={erro} salvando={salvando} rotuloConfirmar="Enviar inscrição" onConfirmar={enviar} onFechar={onFechar}>
      <div className={styles.grid}>
        <div className={`${styles.field} ${styles.cheio}`}>
          <label htmlFor="n-cpf">CPF</label>
          <input id="n-cpf" className={styles.input} inputMode="numeric" placeholder="000.000.000-00" value={form.cpf}
            onChange={e => campo('cpf', aplicarMascaraCpf(e.target.value))} />
          <span className={styles.mutado}>Se a pessoa já existir (por CPF), o cadastro dela é reaproveitado.</span>
        </div>
        <div className={`${styles.field} ${styles.cheio}`}>
          <label htmlFor="n-nome">Nome completo</label>
          <input id="n-nome" className={styles.input} value={form.nome} onChange={e => campo('nome', e.target.value)} />
        </div>
        <div className={styles.field}>
          <label htmlFor="n-nasc">Nascimento</label>
          <DataInput id="n-nasc" value={form.nascimento} onChange={v => campo('nascimento', v)} anoMin={1900} anoMax={anoTemporada} />
        </div>
        <div className={styles.field}>
          <label htmlFor="n-sexo">Sexo</label>
          <select id="n-sexo" className={styles.select} value={form.sexo} onChange={e => campo('sexo', e.target.value as Sexo)}>
            <option value="Masculino">Masculino</option>
            <option value="Feminino">Feminino</option>
          </select>
        </div>
        <div className={styles.field}>
          <label htmlFor="n-email">E-mail</label>
          <input id="n-email" className={styles.input} type="email" value={form.email} onChange={e => campo('email', e.target.value)} />
        </div>
        <div className={styles.field}>
          <label htmlFor="n-tel">Telefone</label>
          <input id="n-tel" className={styles.input} inputMode="tel" placeholder="(71) 99999-9999" value={form.telefone}
            onChange={e => campo('telefone', aplicarMascaraTelefone(e.target.value))} />
        </div>
        <div className={styles.field}>
          <label htmlFor="n-alt">Altura (cm)</label>
          <input id="n-alt" className={styles.input} inputMode="numeric" value={form.alturaCm}
            onChange={e => campo('alturaCm', e.target.value.replace(/\D/g, '').slice(0, 3))} />
        </div>
        <div className={styles.field}>
          <label htmlFor="n-peso">Peso (kg)</label>
          <input id="n-peso" className={styles.input} inputMode="numeric" value={form.pesoKg}
            onChange={e => campo('pesoKg', e.target.value.replace(/\D/g, '').slice(0, 3))} />
        </div>
        <div className={styles.field}>
          <label htmlFor="n-pos">Posição</label>
          <select id="n-pos" className={styles.select} value={form.posicao} onChange={e => campo('posicao', e.target.value)}>
            <option value="">—</option>
            {POSICOES.map(p => <option key={p} value={p}>{p}</option>)}
          </select>
        </div>
        <div className={styles.field}>
          <label className={styles.check} htmlFor="n-plano" style={{ marginTop: 22 }}>
            <input id="n-plano" type="checkbox" checked={form.possuiPlano} onChange={e => campo('possuiPlano', e.target.checked)} />
            Possui plano de saúde
          </label>
        </div>
        {form.possuiPlano && (
          <div className={`${styles.field} ${styles.cheio}`}>
            <label htmlFor="n-nplano">Nome do plano</label>
            <input id="n-nplano" className={styles.input} value={form.nomePlano} onChange={e => campo('nomePlano', e.target.value)} />
          </div>
        )}
      </div>

      <fieldset className={styles.fieldset}>
        <legend className={styles.legenda}>Categorias pretendidas</legend>
        {categorias.map(c => (
          <label key={c.id} className={styles.catOpt}>
            <input type="checkbox" checked={selecionadas.includes(c.id)} onChange={() => alternar(c.id)} />
            <span>
              {c.nome}{' '}
              <span className={styles.catMeta}>
                · {c.idadeMinima > 0 ? `${c.idadeMinima}+` : 'qualquer idade'} · {c.sexo ?? 'mista'}
              </span>
            </span>
            <span className={styles.catValor}>{c.valor == null ? 'a definir' : formatarMoeda(c.valor)}</span>
          </label>
        ))}
        <p className={styles.dica}>Só aparecem categorias vinculadas a esta temporada. O valor final é cobrado depois do fim das inscrições, com desconto para quem joga mais de uma.</p>
        {avisos.length > 0 && (
          <div className={styles.aviso}>
            Fora do esperado: {avisos.join('; ')}. A diretoria decidirá se aprova como exceção (a justificativa é dela).
          </div>
        )}
      </fieldset>

      <label className={styles.check}>
        <input type="checkbox" checked={form.lgpd} onChange={e => campo('lgpd', e.target.checked)} />
        <span>Declaro que li e consinto com o tratamento dos dados pessoais conforme a LGPD (o instante do aceite é registrado).</span>
      </label>
    </Modal>
  );
}
