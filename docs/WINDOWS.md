# Windows: atualizar, testar, gerar e jogar

Esta entrega estabiliza o protótipo de campo e bola das Fases 0/1A. Ainda não contém jogador controlável ou uma partida 11×11.

## Uso simples

1. Feche as janelas do Unity.
2. Baixe e abra `GERAR_E_JOGAR.exe` deste repositório.
3. Aguarde a preparação, os testes e a compilação. O jogo abre no sucesso.

**`GERAR_E_JOGAR.exe` é o preparador Windows, não um player Unity já compilado.** Ele usa o Unity **6000.0.62f1** instalado e ativado no seu computador, com suporte de build Windows Mono. Na primeira execução, precisa de internet. Não exige SDK .NET para o usuário; o preparador usa .NET Framework 4.8 do Windows.

O preparador consulta a branch `codex/phase-0-1a`, baixa uma revisão fixa por SHA e mantém cada versão em `%LOCALAPPDATA%/Wynnyng/versions/<sha>/`. O clone original permanece preservado. Na mesma revisão, um player já gerado com relatório de sucesso é aberto diretamente.

## O que é feito automaticamente

1. Preparação do projeto: TMP Essential Resources, URP, cenas e configurações.
2. Testes Unity **EditMode**. Um XML sem casos aprovados ou com falhas interrompe a geração.
3. Testes Unity **PlayMode**, também com verificação de saída e XML.
4. Build Windows x86_64/Mono pelo `BuildPipeline.BuildPlayer`, com verificação de resultado, erros, EXE, `UnityPlayer.dll` e pasta `_Data`.
5. Criação do atalho **Jogar Wynnyng** na área de trabalho, quando ela estiver disponível, e abertura do jogo.

O teste de pixels exige Editor gráfico/Game visível e é ignorado no batch. A geração automática não é prova de inspeção visual ou de 60 FPS na GTX 1060.

O player real será `Builds/Windows/Wynnyng.exe` dentro da revisão baixada. O atalho aponta para ele e permite jogar depois sem iniciar o Editor. Mantenha a pasta do player completa, com as DLLs e `Wynnyng_Data`. O botão **Sair** encerra o aplicativo; no Editor, encerra o Play.

## Se houver uma falha

A janela mostra a etapa que falhou. Os logs, XMLs dos testes e `build-result.json` ficam em `work/WindowsBuild/` dentro da revisão. O preparador não produz um recibo de sucesso sem os arquivos do player. Se faltar o Editor ou o suporte Windows, a mensagem indica a instalação pelo Unity Hub.

Em caso de saída do Unity com erro, o script também mostra os diagnósticos encontrados no final do log, incluindo erros `CSxxxx`, sem exigir abrir o Bloco de Notas. Depois de uma correção publicada, basta executar novamente o mesmo preparador: ele consulta a revisão atual e baixa os arquivos corrigidos automaticamente.

Nenhum Editor Unity ou licença estava disponível no ambiente que produziu esta entrega. O player do jogo não foi compilado aqui. O preparador foi compilado separadamente; seus testes de argumentos, arquivos e download não equivalem a testar o jogo.

## Uso pelo clone existente

Também é possível executar `GERAR_E_JOGAR.cmd` na raiz do clone com o Editor fechado. Ele usa os arquivos dessa pasta e chama `tools/Build-and-Play.ps1`.

Opções para desenvolvimento:

```powershell
./tools/Build-and-Play.ps1 -EditorPath 'C:/Program Files/Unity/Hub/Editor/6000.0.62f1/Editor/Unity.exe'
./tools/Build-and-Play.ps1 -NoLaunch
```

`-SkipTests` existe somente como opção explícita e não é usado pelo preparador. Pelo Editor, **Wynnyng → Gerar EXE Windows** prepara e compila a cena; os testes completos são parte do script externo.

Para recompilar o preparador a partir do código, use SDK .NET 8:

```powershell
dotnet build tools/WindowsBootstrap/WindowsBootstrap.csproj -c Release -o work/bootstrap
Copy-Item work/bootstrap/GERAR_E_JOGAR.exe .
Copy-Item work/bootstrap/GERAR_E_JOGAR.exe.config .
./GERAR_E_JOGAR.exe --self-test
pwsh -File tools/Test-BuildPipeline.ps1
```

Os testes do preparador não iniciam Unity e não geram um player. Os testes Windows da CI verificam tanto o binário publicado quanto uma compilação nova dos fontes.
