using Efocus.Sitecore.Renderers;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Mvc.Names;
using Sitecore.Mvc.Pipelines.Response.GetRenderer;
using Sitecore.Mvc.Presentation;

namespace Efocus.Sitecore.Pipelines
{
    public class GetUserControlRenderer : GetRendererProcessor
    {
        public override void Process(GetRendererArgs args)
        {
            Assert.ArgumentNotNull((object)args, "args");
            if (args.Result != null)
                return;
            args.Result = this.GetRenderer(args.Rendering, args);
        }

        static readonly ID webControlId = new ID("{1DDE3F02-0BD7-4779-867A-DC578ADF91EA}");
        protected virtual Renderer GetRenderer(Rendering rendering, GetRendererArgs args)
        {
            Item obj = rendering.Item;
            if (obj == null)
                return null;
            if (rendering.RenderingType == "Item")
                return null;
            Template renderingTemplate = args.RenderingTemplate;
            if (renderingTemplate == null)
                return null;
            if (!renderingTemplate.DescendsFromOrEquals(TemplateIDs.Sublayout) &&
                !renderingTemplate.DescendsFromOrEquals(webControlId))
                return null;

            return new UserControlRenderer()
            {
                Rendering = rendering,
                RenderingTemplate =  renderingTemplate
            };
        }
    }
}
