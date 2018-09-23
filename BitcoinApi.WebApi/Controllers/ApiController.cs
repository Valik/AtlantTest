using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using BitcoinApi.Business;
using BitcoinApi.WebApi.Model;

namespace BitcoinApi.WebApi.Controllers
{
    public class ApiController : System.Web.Http.ApiController
    {
        [HttpGet]
        public async Task<IHttpActionResult> GetLast()
        {
            var result = await new BitcoinService().GetLast();
            return Ok(new
            {
                result.success,
                result.errorMessage,
                result.trasactions,
            });
        }

        [HttpPost]
        public async Task<IHttpActionResult> SendBtc([FromBody]SendBtcModel model)
        {
            if (!Validate(model, out var amount, out var errorMessage))
            {
                return BadRequest(errorMessage);
            }

            var result = await new BitcoinService().SendBtc(model.Address, amount);
            return Ok(new
            {
                result.success,
                result.errorMessage,
            });
        }

        private bool Validate(SendBtcModel model, out decimal amount, out string errorMessage)
        {
            errorMessage = string.Empty;
            amount = 0;
            if (!ModelState.IsValid)
            {
                errorMessage = string.Join(", ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
                return false;
            }

            // Unfortunately ModelState.IsValid == true when no parameters passed
            if (model == null)
            {
                errorMessage = "Empty parameters passed";
                return false;
            }

            // Please use '.' as decimal point.
            if (model.Amount.IndexOf(',') != -1 ||
                !decimal.TryParse(model.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out amount) ||
                amount <= 0)
            {
                errorMessage = "Incorrect amount parameter passed";
                return false;
            }

            return true;
        }
    }
}
