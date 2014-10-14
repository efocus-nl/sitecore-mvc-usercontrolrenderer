using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Layouts;
using Sitecore.Mvc.Common;
using Sitecore.Mvc.Presentation;
using Rendering = Sitecore.Mvc.Presentation.Rendering;

namespace Efocus.Sitecore.Renderers
{
    public class UserControlRenderer : Renderer
    {
        public override void Render(TextWriter writer)
        {
            var current = ContextService.Get().GetCurrent<ViewContext>();
            var currentWriter = current.Writer;
            try
            {
                current.Writer = writer;
                //in itemvisualization.getrenderings, the context is swithed to shell#lang cookie???
                //so if you're  logged in into sitecore cms, you'll get the renderings in an incorrect language!
                HttpContext.Current.Request.Cookies.Remove("shell#lang");
                new SitecorePlaceholder(Rendering.RenderingItem).RenderView(current);
            }
            finally
            {
                current.Writer = currentWriter;
            }
        }

        public Rendering Rendering { get; set; }
        public Template RenderingTemplate { get; set; }

    }

    class SitecorePlaceholder : ViewUserControl
    {
        private readonly RenderingItem _item;

        public SitecorePlaceholder(RenderingItem item)
        {
            _item = item;
        }

        public RenderingItem Item
        {
            get { return _item; }
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var form = new SitecoreForm();
            this.Controls.Add(form);
            System.Diagnostics.Debug.WriteLine("Pagerenderings: " + global::Sitecore.Context.Page.Renderings.Count);
            var subLayout = global::Sitecore.Context.Page.Renderings.First(rrf => rrf.RenderingID == Item.ID).GetControl();
            form.Controls.Add(subLayout);
        }

        public override void RenderView(ViewContext viewContext)
        {
            var prevHandler = this.Context.Handler;

            using (var containerPage = new PageHolderContainerPage(this))
            {
                try
                {
                    this.Context.Handler = containerPage;
                    if (global::Sitecore.Context.Page == null)
                    {
                        viewContext.Writer.WriteLine("<!-- Unable to use sitecoreplacholder outside sitecore -->");
                        return;
                    }
                    InitializePageContext(containerPage, viewContext);
                    RenderViewAndRestoreContentType(containerPage, viewContext);
                }
                finally
                {
                    this.Context.Handler = prevHandler;
                }
            }
        }
        
        internal static MethodInfo pageContextInitializer = typeof(global::Sitecore.Layouts.PageContext).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static MethodInfo pageContextOnPreRender = typeof(global::Sitecore.Layouts.PageContext).GetMethod("OnPreRender", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo pageContext_page = typeof(global::Sitecore.Layouts.PageContext).GetField("page", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void InitializePageContext(Page containerPage, ViewContext viewContext)
        {
            var pageContext = global::Sitecore.Context.Page;
            if (pageContext == null)
                return;

            pageContext_page.SetValue(pageContext, containerPage);

            var exists = pageContext.Renderings != null && pageContext.Renderings.Count > 0;
            if (!exists)
            {
                //use the default initializer:
                pageContextInitializer.Invoke(pageContext, null);
                //viewContext.HttpContext.Items["_SITECORE_PLACEHOLDER_AVAILABLE"] = true;
            }
            else
            {
                //our own initializer (almost same as Initialize in PageContext, but we need to skip buildcontroltree, since that is already availabe)
                containerPage.PreRender += (sender, args) => pageContextOnPreRender.Invoke(pageContext, new[] { sender, args });
                switch (Settings.LayoutPageEvent)
                {
                    case "preInit":
                        containerPage.PreInit += (o, args) => pageContext.Build();
                        break;
                    case "init":
                        containerPage.Init += (o, args) => pageContext.Build();
                        break;
                    case "load":
                        containerPage.Load += (o, args) => pageContext.Build();
                        break;
                }
            }
        }

        internal static void RenderViewAndRestoreContentType(ViewPage containerPage, ViewContext viewContext)
        {
            // We need to restore the Content-Type since Page.SetIntrinsics() will reset it. It's not possible
            // to work around the call to SetIntrinsics() since the control's render method requires the
            // containing page's Response property to be non-null, and SetIntrinsics() is the only way to set
            // this. 
            string savedContentType = viewContext.HttpContext.Response.ContentType;
            containerPage.RenderView(viewContext);
            viewContext.HttpContext.Response.ContentType = savedContentType;
        }


        internal sealed class PageHolderContainerPage : ViewPage
        {
            private readonly ViewUserControl _userControl;

            public PageHolderContainerPage(ViewUserControl userControl)
            {
                _userControl = userControl;
            }

            public override void ProcessRequest(HttpContext context)
            {
                this._userControl.ID = "uc_"+NextId();
                this.Controls.Add((Control)this._userControl);
                base.ProcessRequest(context);
            }

            internal string NextId()
            {
                var currentId = Context.Items.Contains("PageHolderContainerPage.nextId") ? (int)Context.Items["PageHolderContainerPage.nextId"] : 1;
                return (Context.Items["PageHolderContainerPage.nextId"] = (currentId + 1)).ToString();
            }

        }
    }

    class SitecoreForm : HtmlForm
    {
        protected override void AddedControl(Control control, int index)
        {
            base.AddedControl(control, index);
            var reference = global::Sitecore.Context.Page.GetRenderingReference(control);
            if (reference != null)
                reference.AddedToPage = true;
        }

        protected override void Render(HtmlTextWriter output)
        {
            if (Controls.Count > 0)
                base.Render(output);
        }
    }
}