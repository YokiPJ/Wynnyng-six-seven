# Validação da entrega — Fases 0 + 1A

## Executado neste ambiente

Ambiente Linux, .NET SDK **8.0.428**, sem Unity Editor instalado/licenciado. Inspeção inicial do repositório remoto: vazio, sem commits. Todos os arquivos desta entrega são novos.

| Verificação | Resultado real |
|---|---|
| Compilação do núcleo | **Passou**, C# 9, .NET Standard 2.1, warnings tratados como erros |
| Testes do núcleo em .NET 8 | **42 passaram, 0 falhas, 0 ignorados** |
| Alocação do núcleo após aquecimento | **0 bytes gerenciados** em 60.000 passos, incluindo física pura, sessão, RNG e publicação de eventos |
| Sintaxe dos scripts de Assets | **29 arquivos**, verificação Roslyn/C# 9; não verifica tipos do motor |
| Estrutura do projeto | **60 GUIDs**, 8 assemblies sem ciclos, **69 arquivos YAML** e referências verificadas |
| Regressões Python | **13 passaram**, incluindo o formato inválido do TagManager e schema de layers |
| Script de build | AST e testes de quoting, descoberta e gates XML passaram em PowerShell 7.4.6; sem executar Unity |
| Preparador Windows | Compilado .NET Framework 4.8 sem warnings/erros; self-tests de quoting, ZIP traversal e recibo passaram; não é o player Unity |
| APIs antigas / código incompleto | Sem usos de Input legado, `FindObjectOfType`, `FindObjectsOfType`, LINQ ou marcadores de implementação pendente nos scripts |
| Espaços/diff | `git diff --check` sem problemas; verificação repetida após adicionar os arquivos ao índice |

Os 42 testes incluem **41 casos compartilhados com o EditMode** e **1 teste adicional de alocação** do runner .NET. O teste de alocação não cobre PhysX, renderização, Input System, logging interno do Editor ou o funcionamento interno do TMP/uGUI.

Comandos reproduzíveis:

```sh
python3 tools/validate_project.py
python3 -m unittest discover -s tools -p 'test_*.py'
dotnet test tools/HeadlessChecks/HeadlessChecks.csproj --configuration Release
```

A CI em `.github/workflows/core-tests.yml` também executou com **sucesso** no GitHub: [Core CSharp tests, execução 43674594509](https://github.com/YokiPJ/Wynnyng-six-seven/actions/runs/43674594509), referente ao commit de código `94017c7d4df46b2b4788ff29a020bd02b66194ca`. O XML TRX fica no artefato `core-test-results` dessa execução. Essa CI não exige licença Unity e não executa o motor.

## Correção após a abertura do usuário

O screenshot de **Package Manager Error** confirmou uma falha do manifest inicial: `com.unity.modules.screen-capture@1.0.0` não existe. Foi corrigido para [`com.unity.modules.screencapture@1.0.0`](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.modules.screencapture.html).

As páginas do manual Unity 6 dos outros 23 módulos declarados foram consultadas e existem sob os nomes usados. Isso confirma nomenclatura; a resolução efetiva dentro do Editor continua sendo um aceite separado. A CI anterior verificava somente os cinco pacotes principais, sem verificar os nomes dos módulos embutidos, e por isso deixou o erro passar.

O validador agora inclui nomes e versões dos módulos. Quatro testes Python verificam a rejeição do erro original, a aceitação do nome correto, uma versão inválida e o manifest entregue. O README descreve a correção local de uma linha e como abrir a cena certa após a resolução dos pacotes. Nenhum sistema da Fase 1B foi iniciado.

## Correção após o log de inicialização do usuário

O log enviado confirmou: settings/fontes TMP ausentes; exceção no getter estático `TMP_Settings.defaultFontAsset`; 1.619 erros subsequentes em `LaboratoryHud.Update`; e quatro tentativas de importar o pacote durante Play. A ausência de TMP e a falta de proteção contra atualização de um HUD parcial são falhas distintas, ambas tratadas nesta correção.

- `TmpResources` carrega os settings pela API null-safe `LoadDefaultSettings` e só depois lê a fonte. A assinatura foi conferida nas fontes oficiais uGUI/TMP.
- O bootstrap verifica TMP antes de criar objetos ou alterar configurações globais. `IsInitialized` só é verdadeiro depois da inicialização completa; exceções liberam o mundo parcial e restauram os globais.
- O HUD recebe a fonte por parâmetro e não atualiza nem abre menus antes de concluir a construção.
- A preparação aguarda a importação e confirma a fonte antes da mensagem Ready. Abrir o laboratório por menu com recursos ausentes inicia a preparação em vez de abrir uma cena que já falharia.
- Três novos casos EditMode cobrem settings ausentes sem chamar o getter, fonte ausente e recursos preparados. Três novos PlayMode cobrem HUD rejeitado sem erros por frame, apresentação completa/câmera e nova tentativa do bootstrap após falha.

Os novos testes de Unity foram escritos e verificados sintaticamente, mas **não foram executados neste ambiente**. A solução imediata para a cópia do usuário é parar o Play, importar TMP Essential Resources, aguardar e preparar novamente. A renderização e o restante do aceite precisam de nova execução no Editor.

## HUD ativo com campo invisível

A screenshot seguinte mostra o HUD/clock ativos e apenas o fundo da câmera. A árvore de pastas não mostra Rendering, criada pela preparação, o que sugere URP ainda não configurado. Essa hipótese não foi confirmada por leitura das configurações Graphics/Quality no PC do usuário.

- O startup agora exige um UniversalRenderPipelineAsset efetivo antes de criar a apresentação. Considera overrides de qualidade e orienta executar a preparação se o pipeline estiver ausente/incompatível.
- A câmera recebe configuração explícita de perspectiva, culling, Base e renderer. Cinemachine é inicializado antes de ativar o Brain usando APIs públicas de 3.1.3.
- O menu Preparar e iniciar laboratorio encadeia importação/preparação, abertura da cena e Play depois da confirmação de recursos.
- Dois testes EditMode verificam pipeline ausente e configuração preparada. Um PlayMode gráfico compara a imagem do gramado ao fundo em quatro posições, para não aceitar apenas o HUD como prova de renderização 3D. Ele é ignorado em batch/nographics e exige Game visível.

**Os testes do motor e de pixels continuam não executados aqui.** A verificação de sintaxe/estrutura e a CI do núcleo não comprovam que o campo foi renderizado.

## TagManager e geração Windows

O erro de parse mostrado na linha 41 do TagManager ocorreu com entradas de layer vazias como `  -`. Elas foram substituídas por strings explícitas `  - ""`. A validação passa pelos arquivos de settings/assets/cena/metas e rejeita esse formato, chaves repetidas e mudanças dos índices de layers. Os outros arquivos YAML presentes passaram nessa verificação genérica.

Foram reforçadas as configurações não finitas e o contrato de transform do bootstrap, além do botão Sair. O pipeline Windows prepara, testa e usa BuildPipeline, mas precisa do Editor instalado/ativado no PC. O preparador `GERAR_E_JOGAR.exe` baixa a revisão atual e automatiza essas etapas. **Não há player do jogo gerado neste ambiente**, pois não há Unity nem licença disponível. Os testes da CI Windows são do preparador/scripts.

## Preparado, sem execução neste ambiente

| Aceite | Como verificar |
|---|---|
| Compilação Unity 6 completa, sem erros/warnings | Abrir no Editor 6000.0.62f1; aguardar importação e conferir Console |
| Resolução efetiva dos pacotes | Package Manager; primeiro import gera `packages-lock.json` |
| EditMode | Test Runner: 61 casos esperados, incluindo validação de valores não finitos |
| PlayMode | Test Runner: 14 testes, incluindo transform inválido e um caso gráfico ignorado em batch |
| Player Windows | O preparador gera no PC usando o Unity instalado; não foi gerado/executado aqui |
| HLSL, materiais e layout da interface | Inspeção em Play no Editor com renderização habilitada |
| Hotplug, remapeamento e dois controles físicos | Checklist manual do README |
| 60 FPS / GTX 1060 / GC da integração | Development Build Windows e Profiler após aquecimento |

**O aceite do Unity permanece aberto.** Testes .NET e inspeção de fontes não equivalem a compilar/executar o projeto no Editor. Não há medição de GPU, draw calls, FPS ou PhysX feita nesta entrega.

## APIs conferidas em fontes oficiais

As assinaturas principais foram consultadas; as fontes das branches oficiais podem evoluir. Isso reduz risco de API inventada, mas não substitui a compilação com os binários do alvo.

- Versão/revisão do Editor: [API de releases Unity 6000.0.62f1](https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.0.62f1&limit=1).
- `Rigidbody.linearVelocity`, `linearDamping`, `angularDamping`: [UnityCsReference, Rigidbody](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/Physics/ScriptBindings/Rigidbody.bindings.cs).
- `PhysicsMaterial` e `PhysicsMaterialCombine`: [UnityCsReference, PhysicsMaterial](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/Physics/ScriptBindings/PhysicsMaterial.bindings.cs).
- `SimulationMode.FixedUpdate`: [UnityCsReference, Physics](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/Physics/ScriptBindings/Physics.bindings.cs).
- `GraphicsSettings.defaultRenderPipeline`: [UnityCsReference, GraphicsSettings](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Export/Graphics/GraphicsSettings.bindings.cs).
- `UniversalRenderPipelineAsset.Create`, `msaaSampleCount`, `shadowCascadeCount`, `useSRPBatcher`: [URP 6000.0](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs).
- `CinemachineCamera.Lens` e `Unity.Cinemachine`: fontes do [pacote oficial Cinemachine 3.1.3](https://download.packages.unity.com/com.unity.cinemachine/-/com.unity.cinemachine-3.1.3.tgz), consultadas fora do projeto.
- Rebinding, filtros de devices e `InputSystemUIInputModule.AssignDefaultActions`: fontes do [pacote oficial Input System 1.11.2](https://download.packages.unity.com/com.unity.inputsystem/-/com.unity.inputsystem-1.11.2.tgz), consultadas fora do projeto.
- Overloads de `TMP_Text.SetText`: [fontes oficiais uGUI/TMP](https://github.com/Unity-Technologies/uGUI/blob/6000.0/com.unity.ugui/Runtime/TMP/TMP_Text.cs).

As fontes consultadas não foram copiadas para o repositório. A entrega usa APIs públicas e implementação própria de simulação, geometria e integração.
