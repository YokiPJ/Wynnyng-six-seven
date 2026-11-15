# HANDOFF — Wynnyng, Fases 0 + 1A

## Contrato com o usuário

Português, iniciante em Unity/C#. Unity 6, URP, PC/gamepad, futuro 11×11 e 1–2 jogadores locais. Não tem Blender nem Mixamo. Fazer uma fase por vez; esta solicitação autorizou **Fase 0 e Fase 1A juntas**. **Não começar a Fase 1B até o usuário dizer “próxima” / “próxima fase”.**

Pedido posterior explícito: “Faça uma super correção no jogo, teste, aprimore e faça um exe pra jogar direto no pc.” Foi autorizada a revisão ampla do protótipo e o pipeline Windows. O conteúdo continua sendo o laboratório 0/1A, não um jogo 11×11 pronto. Não apresentar o preparador `.exe` como se fosse o player Unity.

## Revisão ampla e build Windows

O log real do preparador na revisão 9ebb64a chegou ao compilador C# do Unity e abortou com CS0266 em `WindowsBuild.cs` linhas 108/109. `BuildSummary.totalErrors` e `.totalWarnings` são `int` no Unity 6000.0.62f1; o recibo estava incorretamente declarado `uint`. Ambos os campos foram corrigidos para `int`, sem cast. A licença foi resolvida/atualizada antes da compilação no log; não atribuir essa falha a licença, rede ou espera. O runner agora extrai erros do log para o console e tem regressão PowerShell baseada nesse diagnóstico. Executar o mesmo preparador novamente baixa o HEAD corrigido em outra revisão; não requer editar o clone ou baixar outro EXE.

Na revisão f6a28cc, o usuário confirmou pelo console: preparação concluída em 290 s e **61 EditMode aprovados** em 25 s. PlayMode produziu 14 casos e agregado `Skipped:Ignored`; o script anterior bloqueou esse agregado antes do build. O XML completo do usuário não foi recebido, portanto não afirmar quantos casos individuais PlayMode passaram. Foi reproduzido separadamente com o DLL oficial `com.unity.ext.nunit` 2.0.3: 13 resultados de folhas aprovados + 1 ignorado geram `Skipped:Ignored` para a suíte. Essa reprodução de agregação não executa testes Unity.

O gate agora verifica cada caso e as suítes. Aceita somente a exceção prevista de PlayMode: `Wynnyng.Tests.PlayMode.Phase01AAcceptanceTests.GraphicalPlayerRendersGrassInsteadOfOnlyTheClearBackground`, com resultado Skipped, label Ignored e a mensagem exata sobre Editor gráfico. Exige pelo menos um caso aprovado; bloqueia outros ignorados, inconclusivos, falhas e suítes com falha, inclusive OneTimeTearDown. Mostra contagens separadas e os nomes/mensagens de falhas. Regressões cobrem 13 aprovados + 1 visual ignorado e relatórios inválidos. A CI também verifica Windows PowerShell 5.1, usado pelo preparador, além de PowerShell 7. Build/player e inspeção visual ainda precisam de confirmação no PC.

- `TagManager.asset` tinha itens de layer vazios como `  -`, rejeitados pelo Unity mostrado na screenshot. Agora usa `  - ""`, com exatamente 32 strings e Ground/Goal/Ball em 8/9/10.
- O validador Python usa PyYAML 6.0.3, normaliza somente headers Unity e rejeita itens vazios sem valor, chaves duplicadas e schema de layers incorreto. Há 13 testes Python; isso não comprova o parser nativo do Unity.
- Configs validam finitude de contatos, damping, clips, cores, vetores e cenários. Foram adicionados 12 casos EditMode para NaN/Infinity. Bootstrap exige transform identidade e possui um PlayMode de regressão para posição/rotação/escala incorretas.
- HUD possui Sair, que encerra o player ou o Play no Editor. Core continua sem dependência no motor.
- `ProjectPreparation.PrepareForBuild(Action, Action<Exception>)` conclui após preparação async/TMP/URP/SaveAssets; reporta falha/cancelamento e não entra em Play nem encerra o Editor por conta própria.
- `WindowsBuild.BuildBatch` usa BuildPipeline para StandaloneWindows64/Mono, escreve `work/WindowsBuild/build-result.json` e exige EXE/DLL/_Data antes de declarar sucesso.
- `tools/Build-and-Play.ps1` encontra o Unity instalado, prepara, executa EditMode e PlayMode, valida XMLs e gera/abre o player. Testes habilitados por padrão. `GERAR_E_JOGAR.cmd` usa o clone atual.
- `GERAR_E_JOGAR.exe` é um preparador gerenciado Windows (.NET Framework 4.8), compilado por SDK .NET 8 a partir de `tools/WindowsBootstrap/`. Baixa o HEAD da branch por SHA para LocalAppData/Wynnyng/versions, preservando o clone; chama o script usando o Unity do PC. Reutiliza build completo na mesma revisão. O script cria atalho Jogar Wynnyng após sucesso.
- O ambiente não tem Unity/ativação, portanto NÃO existe player Unity compilado aqui. O EXE entregue é o preparador que produz o jogo no PC do usuário. A CI Windows testa o preparador e o script, não o jogo Unity.
- Total preparado: **61 casos EditMode, 14 PlayMode** (um gráfico ignorado em batch). Núcleo: 42 testes headless; Python: 13. Não registrar os casos Unity como aprovados sem executar no Editor.

Veja `docs/WINDOWS.md` para comandos, logs e limitações. A falha de qualquer etapa impede abrir um player novo como se o build tivesse passado.

Sensação desejada: arcade-sim de futebol clássico, com conteúdo original. Não copiar código, modelos, som, menus ou dados de outro jogo. Dados reais devem vir de CSV/JSON; elencos de exemplo devem ser explicitamente `isPlaceholder = true`. Nenhum dado de jogador/clube foi criado nesta entrega.

## Origem e versões

- Repositório: https://github.com/YokiPJ/Wynnyng-six-seven . Estava vazio, sem commits, arquivos ou AGENTS.md na inspeção inicial.
- Implementação preparada em `codex/phase-0-1a`.
- Unity **6000.0.62f1**, revisão **f99f05b3e950**, versão confirmada na API pública de releases da Unity.
- URP **17.0.4**, Input System **1.11.2**, Cinemachine **3.1.3**, uGUI **2.0.0** com TMP, Test Framework **1.4.5**.
- Novo backend ativo: `activeInputHandler: 1`. Sem `UnityEngine.Input` legado, `.velocity`/`.drag` antigos ou namespace Cinemachine 2.
- Correção após a primeira abertura do usuário: o manifest inicial tinha `com.unity.modules.screen-capture`, nome inválido. O correto é **`com.unity.modules.screencapture`**, versão 1.0.0. Os outros 23 módulos declarados foram conferidos nas páginas do manual Unity 6. O validador agora verifica nomes/versões de módulos e tem 4 testes de regressão Python executados na CI. Isso não equivale a resolver pacotes dentro do Editor.
- Correção após o log de Play: TMP settings/fontes ausentes causaram `TMP_Settings.get_defaultFontAsset` e o HUD parcialmente inicializado repetiu `NullReferenceException` por frame. O log também confirmou tentativas de importar TMP em Play (`Cannot import package in play mode`). Orientar parar Play, importar **Window > TextMeshPro > Import TMP Essential Resources**, aguardar e preparar novamente. `TmpResources` usa `TMP_Settings.LoadDefaultSettings()` antes do getter da fonte; o bootstrap verifica dependências antes de criar o mundo, e o HUD só atualiza após inicialização completa. Preparação só informa Ready após confirmar a fonte.
- Após a importação de TMP, o usuário mostrou HUD ativo com o campo ainda invisível. A pasta Rendering não aparece na árvore da screenshot, indício de preparação URP ausente; não foi possível ler Graphics/Quality no PC para confirmar a configuração. `RenderingResources` agora verifica a seleção efetiva (override de qualidade, depois Graphics) antes do startup visual. A câmera usa Base/renderer padrão, culling explícito e prepara o estado Cinemachine antes de habilitar seu Brain. Existe um teste gráfico de pixels; a correção visual ainda precisa ser executada/confirmada no Unity.
- Manifest versionado. `packages-lock.json` será produzido pelo primeiro import no Editor, sem inventar uma resolução não executada.

## Estado de aceite

Implementação das Fases 0/1A entregue em arquivos completos. O ambiente de desenvolvimento **não possuía o Unity Editor**.

- Núcleo compilado com C# 9 e .NET Standard 2.1; testes compartilhados executados em .NET 8.
- Verificação estrutural de GUIDs, asmdefs, ações e referências executada.
- Sintaxe de todos os scripts verificada por Roslyn; isso não confere tipos/APIs do motor.
- **Compilação no Unity, ausência de warnings no Editor, shaders, EditMode/PlayMode e desempenho permanecem pendentes de execução.** Não descrever como executados ou garantir 60 FPS.
- Resultado e contagens finais: `docs/VALIDACAO.md`. Não substituir as evidências por suposições.

## Abrir e testar

1. Unity Hub → Add project from disk → pasta com Assets/Packages/ProjectSettings.
2. Abrir com 6000.0.62f1 e aguardar pacotes.
3. Wynnyng → Preparar Fases 0 e 1A; aguardar Ready. Importa TMP Essential Resources do próprio pacote uGUI e cria assets de URP.
4. Wynnyng → Abrir laboratorio Fase 1A → Play, Game 16:9.
5. D-pad escolhe cenário, RB lança, Select/View repõe; teclado setas/Enter/R. Start/F1 abre controles.
6. Window → General → Test Runner → Run All em EditMode e PlayMode.

Atalho novo: com o Play parado, **Wynnyng → Preparar e iniciar laboratorio** importa os recursos quando necessário, configura URP, abre a cena e entra em Play após Ready. Importação cancelada/falha cancela o início. Abertura normal pelo menu também verifica se o URP está configurado. Quando o HUD aparece sem campo, orientar a preparação e confirmar a pasta `Assets/Wynnyng/Rendering/`; não tratar HUD ou clock ativo como prova de renderização do mundo.

Se estiver usando o ZIP inicial, corrigir essa única chave em `Packages/manifest.json` com o Editor fechado, salvar e reabrir. O erro de resolução foi observado em screenshot do usuário. Outra screenshot mostra `Untitled` com Main Camera/Directional Light: abrir a cena do laboratório pelo menu antes de esperar campo/bola. Não pedir reinstalação do Unity; a versão mostrada é a correta, 6000.0.62f1.

Menu de preparação é idempotente: preserva SOs/cena existentes e outras cenas de build. Cria assets de renderização em `Assets/Wynnyng/Rendering/`. Se esses assets ou recursos TMP ainda não existirem, executar o menu antes do Play. Não alterar projetos fora deste repositório.

## Arquitetura e dependências

```text
Core                 (sem Unity; noEngineReferences)
  ↑ Configuration    (SOs -> parâmetros/geometry do Core)
  ↑ Input            (Core + Unity.InputSystem)
  ↑ Presentation     (Core + Configuration + Input + CM3 + uGUI/TMP)
  ↑ Runtime          (Core + Configuration + Input + Presentation)
  ↑ Editor           (configuração e bootstrap + APIs do Editor/URP)
Tests                (EditMode/PlayMode separados; excluídos de build normal)
```

O diagrama é uma ordem de camadas, não uma dependência de todas as camadas acima. Os `.asmdef` são a fonte exata; não criar ciclos. Namespace raiz `Wynnyng` com subnamespaces iguais às camadas.

`Core` contém `Vec3`, `BallPhysics`, `PitchGeometry`, `BoundaryEvaluator`, `BoundaryTracker`, `MatchSession`, `SimulationClock`, `SeededRandom`, `EventChannel<T>` e interfaces. Não depende de MonoBehaviour, Time, Rigidbody, Input, UI ou dados de clubes. Os mesmos fontes são compilados em `tools/CoreLibrary/`.

`MatchBootstrap` é a composition root. Carrega configs de Resources ou usa referências do Inspector; valida antes de construir. Cria mundo procedural, bola, sessão, eventos, entrada opcional e apresentação opcional. Um laboratório ativo por vez. Deve ficar na origem com escala 1. Salva/restaura timestep, limite de frames, gravidade, bounce threshold, simulationMode e vSync ao ser destruído. Se houver apresentação, exige URP efetivo; TMP só é exigido quando há HUD/entrada. `RenderingResources` respeita o override de qualidade, mesmo se ele não for URP; Graphics sozinho não garante que o renderer selecionado seja o correto.

`MatchBootstrap.IsInitialized` é um commit de inicialização completa, incluindo o HUD quando habilitado; não depende apenas de a sessão existir. Em exceção, desativa/destrói o mundo parcial, libera os inputs/eventos, remove as referências da sessão/bola/HUD, restaura parâmetros globais e desabilita o componente. É possível configurar/corrigir e tentar `Initialize` novamente. `LaboratoryHud` recebe a fonte explicitamente; fica desabilitado até terminar e `Update`/menu também verificam `IsInitialized`. `TmpResources.TryGetDefaultFont` permite loaders opcionais para testar ausência sem apagar assets do usuário. Não chamar o getter estático da fonte antes de verificar os settings.

`MatchBootstrap.FixedUpdate` consome comandos da bancada, amostra o resultado físico anterior, avança sessão/detector e aplica aceleração para o próximo solve do PhysX. Physics em `SimulationMode.FixedUpdate`, **1/60 s**. Detecção pode ser observada no próximo passo fixo; não depende de triggers. Rigidbody com `Interpolate` para renderização.

`BallBody` adapta Rigidbody/colisores e aplica aceleração aerodinâmica em `ForceMode.Acceleration`. Gravidade pertence ao PhysX. `linearDamping = 0`; não duplicar arrasto nativo e arrasto quadrático. Magnus = coeficiente × spin(rad/s) × velocidade por produto vetorial. Rolamento age no plano XZ quando o raycast de chão e a velocidade vertical indicam contato.

`RuntimeAssets` possui materiais, malhas e PhysicsMaterials criados no startup e os destrói junto do mundo. Usar `sharedMaterial`; não acessar `renderer.material` no loop. Listas/new são permitidos na construção, no menu e nos testes; não criar coleções no passo fixo ou LINQ no loop.

## Convenções geométricas e regras atuais

- Escala: metros; X largura, Z comprimento, Y altura. Centro = origem.
- Campo: 105×68 m, definidos pelas bordas externas das linhas de limite. Gol: abertura livre 7,32×2,44 m; profundidade inicial 2,5 m.
- Bola: raio 0,11 m, massa 0,43 kg. Sem collider extra na malha visual.
- Chão layer 8 (`Ground`), gol layer 9 (`Goal`), bola layer 10 (`Ball`). `WorldLayers` documenta índices estruturais; `TagManager.asset` usa os mesmos nomes.
- Gol/fim só quando centro passa de `halfLength + radius`; lateral quando passa de `halfWidth + radius`.
- Calcula interseção do segmento anterior→atual com todos os planos; usa o primeiro plano. Altura e largura verificadas nesse instante, com esfera inteira dentro da abertura.
- Tolerância de contato de 2 mm somente na abertura/chão, não no cruzamento da linha.
- Detector fica travado depois de um evento até `ResetBall`; rebote na rede não pode contar outro gol.
- Saída detectada não é reinício oficial. Bola reposta no centro depois de 3 s para facilitar testes, não para simular lateral/escanteio/tiro de meta.
- Contadores +Z/−Z são do laboratório e não definem mandante/visitante. Direção de ataque por equipe pertence à Fase 2.

## Eventos, RNG e logging

`EventChannel<T>` tem capacidade fixa (`SimulationConfig.EventCapacity`). Inscrição duplicada é ignorada; canal cheio lança exceção; mudanças de inscrição/remoção e publicação recursiva durante dispatch são proibidas. O callback de diagnóstico apenas armazena status, programa reset e emite mensagem estática.

`MatchSession` recebe `IRandomSource` e `IEventChannel<MatchEvent>` por construtor. `SeededRandom` usa xorshift32; seed zero tem substituição definida. RNG está pronto e injetado, mas os cenários da bancada não consomem aleatoriedade. Não alegar determinismo entre plataformas do PhysX.

Logging por `IGameLog`/`UnityGameLog`, configurável no SO. Não formatar strings/emitir logs a cada passo. Desativar logs para medir GC/performance da integração.

## Entrada e remapeamento

`Resources/Input/Match.inputactions`: mapas Gameplay e Laboratory, grupos Gamepad e Keyboard, IDs estáveis. Um clone de InputActionAsset por slot local; filtro de devices separado; dois slots. Dispositivos ainda conectados são preservados; novos pads ocupam slots vagos; teclado só é fallback de P1. P2 sem pad gera comando neutro.

Gameplay preparado: Move, Shoot, Pass, Through, Lob, Sprint, Switch, Pause. `PlayerCommand` não conhece o motor. **É snapshot do frame de entrada**: a Fase 1B deve adicionar buffering/consumo de bordas uma única vez no passo fixo, evitando repetição quando houver vários FixedUpdate no mesmo frame.

Menu diagnóstico permite rebind por comando e jogador, incluindo partes WASD. Captura somente o dispositivo do slot. Overrides persistem em JSON por jogador no `persistentDataPath`; não salvar em Assets. Cancelar com Esc; botão Padrao restaura. Eventos de hotplug e mudanças de menu podem alocar; o loop estável não cria clones/arrays.

## Configurações e tuning

Todos em `Assets/Wynnyng/Resources/Config/`; editar fora do Play nesta fase.

| SO | Principais valores |
|---|---|
| Simulation | 60 Hz; render target 60; máximo frame 0,1 s; gravity (0,−9,81,0); seed 2026; solver 8/4; bounce threshold 0,2 |
| Pitch | 105×68; gol 7,32×2,44; poste raio 0,06; tolerância 0,002; linhas 0,12; círculos 96 segmentos; net spacing 0,25 |
| Ball | massa 0,43; raio 0,11; Cd 0,25; densidade 1,225; Magnus 0,0015; restituição 0,65; atrito contato 0,18; rolamento 0,015; angular damping 0,05 |
| Presentation | visão (78,72,0) olhando origem, FOV 68; auto reset 3 s; marcador 0,7 m; 8 cenários com posição/velocidade/spin configuráveis |

`BallBody` captura parâmetros aerodinâmicos na inicialização. Mudanças ao vivo de SO não são um painel de tuning confiável nesta entrega. Na Fase 1B, usar cópias de configuração em runtime e atualizar explicitamente os parâmetros do adaptador, sem gravar tuning de teste nos assets por acidente.

Dimensões, contatos e velocidades são tuning; 0/1, metade/diâmetro, índices de topologia, π e constantes do algoritmo RNG são relações/estrutura matemática, não números de gameplay escondidos.

## Sistemas e fases

| Sistema | Estado |
|---|---|
| Pastas/asmdefs/configs/bootstrap/eventos/RNG/logging | Implementado; compilação do motor pendente |
| Input Actions, dois slots e overrides JSON | Implementado; validar dispositivos físicos |
| Campo/linhas/gols/redes procedurais | Implementado; inspeção visual/colisão pendente |
| Bola: Rigidbody/CCD/interpolação/arrasto/Magnus/quique/rolamento | Implementado; PlayMode e tuning no Unity pendentes |
| Gol/fora/latch/reset | Lógica pura testada; integração física pendente |
| Bancada e menu de remapeamento | Implementados como diagnóstico; não são HUD final |
| Jogador, passe, chute, Wide seguindo bola, sliders runtime | **Fase 1B: não iniciar sem “próxima”** |
| 22 jogadores, IA, goleiro, troca, stamina/estatísticas | Fase 2 |
| Regras, árbitro, impedimento, faltas, bolas paradas, substituições | Fase 3 |
| Pipeline Blender, modelos/animações, kits, estádio, torcida | Fase 4 |
| HUD/menus finais, replay, áudio e câmeras extras | Fase 5 |
| Clubes/elencos CSV, competição Série A 2026, tabela, calendário, save | Fase 6 |
| Carreira e edição | Fase 7 |
| Balanceamento, performance, build e QA | Fase 8 |

## Limitações e próximo trabalho autorizado

O projeto ainda não contém um jogo 11×11, nem elenco/rating oficial ou placeholder: esses dados pertencem às fases de times/competição. A lista de 20 clubes fornecida pelo usuário é um seed a conferir em fonte oficial quando a fase de dados chegar. Não afirmar que ela foi verificada nesta fase.

Redes rígidas e traves com cápsulas são blockout. Vista geral Cinemachine fixa serve para ver o campo inteiro; a bola de 22 cm tem um marcador no chão para ser visível à distância. Shaders HLSL são originais e sem marcas. TMP usa recursos fornecidos pelo próprio Unity, importados pelo comando de preparação.

Antes de declarar aceite 0/1A: abrir no Editor correto; preparar; corrigir eventuais erros/warnings reais; rodar EditMode/PlayMode; verificar os oito cenários; medir a integração no Profiler e num PC representativo. **Não contornar essa pendência dizendo que teste .NET valida Rigidbody, shaders ou FPS.**

Após a correção de TMP, são esperados **47 casos EditMode** e **12 testes PlayMode**, incluindo apresentação completa, falha de HUD sem spam e recuperação do bootstrap. Os testes novos dependem do Unity e foram preparados, não executados neste ambiente. O núcleo permanece com 42 testes headless. A importação manual precisa terminar antes do Play; não interpretar o erro antigo de pacote ainda visível na Console como prova de uma nova falha sem limpar/reproduzir.

Após a proteção de URP, o total preparado é **49 casos EditMode e 13 PlayMode**. O teste gráfico compara pixels de quatro pontos de gramado ao fundo da câmera, com Game visível; é ignorado em batch/nographics, pois precisa de renderização real. Não declarar esse teste como passado usando os resultados headless. A confirmação visual do usuário e o teste no Editor seguem pendentes.

Quando o usuário disser “próxima”, implementar apenas 1B: jogador com inércia, passe, chute, câmera Wide ponderada e sliders runtime, preservando todos os contratos/testes anteriores e atualizando este HANDOFF.
