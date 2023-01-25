using Nml.Improve.Me.Dependencies;
using System;
using System.Linq;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Application = Nml.Improve.Me.Dependencies.Application;

namespace Nml.Improve.Me
{
    public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext _dataContext;
		private readonly IPathProvider _templatePathProvider;
        private readonly IViewGenerator _viewGenerator;
		private readonly IConfiguration _configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
		private readonly IPdfGenerator _pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			if (dataContext != null)
				throw new ArgumentNullException(nameof(dataContext));

            _dataContext = dataContext;
			_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException("templatePathProvider");
            _viewGenerator = viewGenerator;
			_configuration = configuration;
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_pdfGenerator = pdfGenerator;
		}

		public byte[] Generate(Guid applicationId, string baseUri)
		{
			string view = String.Empty;
			try
			{
				Application application = _dataContext.Applications.Single(app => app.Id == applicationId);

				if (application == null)
				{
					_logger.LogWarning($"No application found for id '{applicationId}'");
					return null;
				}

				if (baseUri.EndsWith("/"))
					baseUri = baseUri.Substring(baseUri.Length - 1);

				switch (application.State)
				{
					case ApplicationState.Pending:
                        view = PendingApplicationState(application, baseUri);
                        break;
					case ApplicationState.Activated:
                        view = ActivatedApplicationState(application, baseUri);
                        break;
					case ApplicationState.InReview:
                        view = InReviewApplicationState(application, baseUri);
                        break;
					default:
                        _logger.LogWarning($"The application is in state '{application.State}' and no valid document can be generated for it.");
                        return null;
				}

                PdfOptions pdfOptions = new PdfOptions
                {
                    PageNumbers = PageNumbers.Numeric,
                    HeaderOptions = new HeaderOptions
                    {
                        HeaderRepeat = HeaderRepeat.FirstPageOnly,
                        HeaderHtml = PdfConstants.Header
                    }
                };
                PdfDocument pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
                return pdf.ToBytes();
            }
            catch (Exception ex)
			{
                _logger.LogWarning($"Error occured: {ex.Message}");
                return null;
            }
		}

		private string PendingApplicationState(Application application, string baseUri)
        {
            string view;
            try
            {
                string path = _templatePathProvider.Get("PendingApplication");
                PendingApplicationViewModel activatedApplicationViewModel = new PendingApplicationViewModel
                {
                    ReferenceNumber = application.ReferenceNumber,
                    State = application.State.ToDescription(),
                    FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                    AppliedOn = application.Date,
                    SupportEmail = _configuration.SupportEmail,
                    Signature = _configuration.Signature
                };
                view =  _viewGenerator.GenerateFromPath($"{baseUri}{path}", activatedApplicationViewModel);
                return view;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private string ActivatedApplicationState(Application application, string baseUri)
        {
            string view;
            try
            {
                string path = _templatePathProvider.Get("ActivatedApplication");
                ActivatedApplicationViewModel activatedApplicationViewModel = new ActivatedApplicationViewModel
                {
                    ReferenceNumber = application.ReferenceNumber,
                    State = application.State.ToDescription(),
                    FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                    LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                    PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                    PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                                                    .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                                    .Sum(),
                    AppliedOn = application.Date,
                    SupportEmail = _configuration.SupportEmail,
                    Signature = _configuration.Signature
                };
                view = _viewGenerator.GenerateFromPath($"{baseUri}{path}", activatedApplicationViewModel);
                return view;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private string InReviewApplicationState(Application application, string baseUri)
        {
            string view;
            try
            {
                string templatePath = _templatePathProvider.Get("InReviewApplication");
                string inReviewMessage = "Your application has been placed in review" +
                                    application.CurrentReview.Reason switch
                                    {
                                        { } reason when reason.Contains("address") =>
                                            " pending outstanding address verification for FICA purposes.",
                                        { } reason when reason.Contains("bank") =>
                                            " pending outstanding bank account verification.",
                                        _ =>
                                            " because of suspicious account behaviour. Please contact support ASAP."
                                    };
                InReviewApplicationViewModel inReviewApplicationViewModel = new InReviewApplicationViewModel()
                {
                    ReferenceNumber = application.ReferenceNumber,
                    State = application.State.ToDescription(),
                    FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                    LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                    PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                    PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                                        .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                        .Sum(),
                    InReviewMessage = inReviewMessage,
                    InReviewInformation = application.CurrentReview,
                    AppliedOn = application.Date,
                    SupportEmail = _configuration.SupportEmail,
                    Signature = _configuration.Signature
                };

                view = _viewGenerator.GenerateFromPath($"{baseUri}{templatePath}", inReviewApplicationViewModel);
                return view;
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
}

