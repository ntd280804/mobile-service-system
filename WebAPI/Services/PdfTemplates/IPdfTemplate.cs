namespace WebAPI.Services.PdfTemplates
{
    public interface IPdfTemplate<T>
    {
        byte[] GeneratePdf(T dto, PdfTemplateContext context);
    }
}

