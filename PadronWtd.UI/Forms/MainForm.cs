using SAPbouiCOM;
// using PadronSaltaAddOn.Application.Services;

namespace PadronSaltaAddOn.UI.Forms
{
    internal class MainForm
    {
        private readonly Application _app;
        //     private readonly ImpuestoService _impuestoService;
        private Form _form;

        //        public MainForm(Application app, ImpuestoService impuestoService)
        public MainForm(Application app)
        {
            _app = app;
            //       _impuestoService = impuestoService;
            CreateForm();
        }

        private void CreateForm()
        {
            FormCreationParams cp = (FormCreationParams)_app.CreateObject(BoCreatableObjectType.cot_FormCreationParams);
            cp.UniqueID = "frmPadron";
            cp.FormType = "frmPadron";
            cp.BorderStyle = BoFormBorderStyle.fbs_Fixed;

            _form = _app.Forms.AddEx(cp);
            _form.Title = "ACTUALIZACION IMPOSITIVA *SALTA*";
            _form.Width = 430;
            _form.Height = 260;

            Item label = _form.Items.Add("lblOpt", BoFormItemTypes.it_STATIC);
            label.Top = 40; label.Left = 20;
            ((StaticText)label.Specific).Caption = "Opciones:";

            AddButton("btnFecha", "Mantenimiento de Fecha", 70);
            AddButton("btnImp", "Mantenimiento de Impuestos", 110);
            AddButton("btnProc", "Importar y procesar", 150);

            _app.ItemEvent += App_ItemEvent;
            _form.Visible = true;
        }

        private void AddButton(string id, string caption, int top)
        {
            Item btn = _form.Items.Add(id, BoFormItemTypes.it_BUTTON);
            btn.Top = top; btn.Left = 40; btn.Width = 200;
            ((Button)btn.Specific).Caption = caption;
        }

        private void App_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;
            if (pVal.EventType == BoEventTypes.et_ITEM_PRESSED && !pVal.BeforeAction && FormUID == "frmPadron")
            {
                switch (pVal.ItemUID)
                {
                    case "btnImp":
                        _app.MessageBox("Tasa actualizada correctamente");
                        break;
                    case "btnProc":
                        _app.MessageBox($"implementada: {pVal.ItemUID}");
                        OnImportarClick();
                        break;
                    default:
                        _app.MessageBox($"Acción no implementada: {pVal.ItemUID}");
                        break;
                }
            }
        }

        private void OnImportarClick()
        {
            //var frmImportar = new FrmImportar(SBO_Application, oCompany);
            //var frmImportar = new FrmImportar(_app);
            //frmImportar.CreateForm();
        }
    }
}
