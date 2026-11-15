using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wynnyng.Presentation
{
    public static class RenderingResources
    {
        public const string MissingPipelineMessage =
            "URP nao configurado para a qualidade atual. Pare o Play e execute " +
            "Wynnyng > Preparar Fases 0 e 1A. Aguarde Ready antes de iniciar o laboratorio.";

        // A quality override takes precedence over Graphics Settings, including a non-URP override.
        public static bool IsUniversalPipeline(RenderPipelineAsset qualityOverride, RenderPipelineAsset graphicsDefault)
        {
            RenderPipelineAsset selected = qualityOverride != null ? qualityOverride : graphicsDefault;
            return selected is UniversalRenderPipelineAsset;
        }

        public static bool IsConfigured => IsUniversalPipeline(
            QualitySettings.renderPipeline, GraphicsSettings.defaultRenderPipeline);

        public static void RequireConfigured()
        {
            if (!IsConfigured) throw new InvalidOperationException(MissingPipelineMessage);
        }
    }
}
