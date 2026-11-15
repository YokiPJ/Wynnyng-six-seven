# Validação — partida contra a CPU

## Executado no ambiente de desenvolvimento

Linux, SDK .NET 8.0.428, PowerShell 7.4.6, **sem Editor/licença Unity**.

| Verificação | Evidência |
|---|---|
| Core sem motor | Compila C# 9/.NETStandard 2.1 com warnings como erros |
| Testes .NET | **80 aprovados, zero falhas/ignorados** |
| CPU × CPU | 600 s simulados com integrador de bola simplificado; posições/stamina finitas, passes/finalizações e contagens verificadas |
| Alocação do Core | Dois testes de60.000 passos após aquecimento:0 bytes gerenciados na thread; um laboratório e outro partida com22 jogadores |
| Sintaxe Assets | Roslyn/C# 9:44 arquivos,0 erros; não é verificação de tipos do motor |
| Estrutura |77 GUIDs únicos, 8 assemblies sem ciclos, 88 YAMLs verificados |
| Preparador | .NET Framework 4.8 recompilado:0 erros/0 warnings; testes de argumentos,ZIP,recibo e gates separados do Unity |

O teste de600 s produziu, com seed2026: **1:0**, chutes 3:2, passes 226:222, defesas 2:2, posse 119,194 s:109,025 s. Isso comprova que houve ações e que o loop não travou, sem esconder o volume baixo de chutes e a circulação excessiva como risco de balanceamento. Não usar esse resultado como prova de boa jogabilidade ou PhysX equivalente.

Na primeira execução desse teste, a IA não finalizou. A correção preservou a asserção de finalização: perseguidor passa a ser escolhido por interceptação prevista, corre até aproximar e desacelera ao chegar. O teste passou depois da correção. Também foi normalizado o vetor usado no spin do chute, evitando curva multiplicada pela distância do alvo da CPU.

Comandos:

```sh
python3 -m pip install -r tools/requirements-validation.txt
python3 tools/validate_project.py
python3 -m unittest discover -s tools -p 'test_*.py'
dotnet test tools/HeadlessChecks/HeadlessChecks.csproj -c Release
pwsh -File tools/Test-BuildPipeline.ps1
```

## Preparado para executar no Unity

- **103 casos EditMode**: 61 anteriores, 23 FootballCore, 11 de finalização, 3 de buffer e 5 de configuração/cena.
- **21 PlayMode**: 14 anteriores e 7 da partida, incluindo gol com bola, cápsulas e traves do PhysX. O teste antigo de pixels exige Editor gráfico e é o único ignore permitido no batch.
- Os novos casos de integração verificam22 jogadores,2goleiros, bola dinâmica/CCD, progressoCPU, gol único/reinício, pausa/restauração do tempo, tuning isolado e HUD/câmera/menu.
- As fixtures limpam laboratórios e partidas anteriores para não sobrepor mundos/física/configurações globais.

**Esses testes do motor não foram executados aqui.** O log do usuário confirmou61EditMode aprovados na versão anteriorf6a28cc; isso não valida os arquivos novos. O novo preparador executa novamente os testes Unity antes de gerar o player no PC.

## Build e CI

A cena padrão do build é `Assets/Wynnyng/Scenes/FootballMatch.unity`. O laboratório continua na lista do Editor para diagnóstico, mas WindowsBuild fornece somente a cena da partida a BuildPipeline. O preparador exige recibo de sucesso e arquivos completos antes de abrir.

A CI verifica estrutura/Python/Core no Linux e scripts/preparador no Windows, inclusive PowerShell 7 eWindowsPowerShell 5.1. **Não tem Unity/licença e não gera o jogo Unity.** O artefato Windows da CI é o preparador.

## Checklist no player

1. Confirmar22 jogadores/cores/seleção/câmera e adversárioCPU.
2. Mover/correr/passar/profundidade/lançamento/chutecarregado/trocar.
3. Conferir goleiro, placar, reposição de treino e troca de lados.
4. Pausar e verificar congelamento de relógio/bola; continuar sem chute involuntário.
5. Testar sliders/rebind, CPUdemo, dois controles e desconexão deP2.
6. Completar partida e Nova partida; fechar/reabrir.
7. Executar o teste de pixels no Editor com Game visível e medir GC/FPS/GPU no Windows.

## Regressões anteriores mantidas

Manifest corrigido para `com.unity.modules.screencapture`; TMP/URP verificados antes de construir o mundo; HUD parcial não atualiza; TagManager com32layers string; configs rejeitamNaN/Infinity; contadores BuildSummary como int; erros reais extraídos do log; agregadoNUnit Ignored aceito somente para o caso visual previsto. A correção do parser foi reproduzida com o DLL oficial ext.nunit 2.0.3.

Limitações: campo/atletas procedurais sem animaçãoHumanoid, regras/reinícios simplificados e IA em balanceamento. Não há medição de60FPS/GTX1060, inspeção do novo HUD no player, teste de gamepad físico ou execução PhysX feita neste ambiente.

## Regressão de finalizações

O relato do usuário levou a testes que exigem gols pelo percurso real do projétil no integrador puro, com goleiro ativo; não basta a velocidade apontar ao gol. Antes da correção, seis de oito cenários iniciais falharam. Os 11 casos finais passaram sem retirar a defesa de chutes fracos centrais ou aceitar bolas fora das traves como gols.

Foram corrigidos alvo assistido, dispersão, movimento durante a carga e alcance/tempo de reação do goleiro. A suíte inclui finalizações a 6/12/18 m, ambos os cantos e troca de lados. O teste adicional PlayMode usa Physics.Simulate e exige que o Rigidbody da bola faça o gol com o goleiro e os colliders ativos. Ele **não foi executado neste ambiente**; fica obrigatório no fluxo do preparador.
