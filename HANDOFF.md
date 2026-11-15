# HANDOFF — Wynnyng, Fases 0 + 1A

## Contrato com o usuário

Português, iniciante em Unity/C#. Unity 6, URP, PC/gamepad, futuro 11×11 e 1–2 jogadores locais. Não tem Blender nem Mixamo. Fazer uma fase por vez; esta solicitação autorizou **Fase 0 e Fase 1A juntas**. **Não começar a Fase 1B até o usuário dizer “próxima” / “próxima fase”.**

Sensação desejada: arcade-sim de futebol clássico, com conteúdo original. Não copiar código, modelos, som, menus ou dados de outro jogo. Dados reais devem vir de CSV/JSON; elencos de exemplo devem ser explicitamente `isPlaceholder = true`. Nenhum dado de jogador/clube foi criado nesta entrega.

## Origem e versões

- Repositório: https://github.com/YokiPJ/Wynnyng-six-seven . Estava vazio, sem commits, arquivos ou AGENTS.md na inspeção inicial.
- Implementação preparada em `codex/phase-0-1a`.
- Unity **6000.0.62f1**, revisão **f99f05b3e950**, versão confirmada na API pública de releases da Unity.
- URP **17.0.4**, Input System **1.11.2**, Cinemachine **3.1.3**, uGUI **2.0.0** com TMP, Test Framework **1.4.5**.
- Novo backend ativo: `activeInputHandler: 1`. Sem `UnityEngine.Input` legado, `.velocity`/`.drag` antigos ou namespace Cinemachine 2.
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

`MatchBootstrap` é a composition root. Carrega configs de Resources ou usa referências do Inspector; valida antes de construir. Cria mundo procedural, bola, sessão, eventos, entrada opcional e apresentação opcional. Um laboratório ativo por vez. Deve ficar na origem com escala 1. Salva/restaura timestep, limite de frames, gravidade, bounce threshold, simulationMode e vSync ao ser destruído.

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

Quando o usuário disser “próxima”, implementar apenas 1B: jogador com inércia, passe, chute, câmera Wide ponderada e sliders runtime, preservando todos os contratos/testes anteriores e atualizando este HANDOFF.
