using Microsoft.AspNetCore.Mvc;
using CityInfo.API.Models;
using CityInfo.API.Services;
using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Authorization;

namespace CityInfo.API.Controllers
{
	[Route("api/v{version:apiVersion}/cities/{cityId}/pointsofinterest")]
	[ApiController]
	[ApiVersion("2.0")]
	// [Authorize(Policy = "MustBeFromAntwerp")]
	public class PointsOfInterestController : ControllerBase
	{

		private readonly ILogger<PointsOfInterestController> _logger;
		private readonly IMailService _mailService;
		private readonly ICityInfoRepository _repository;
		private readonly IMapper _mapper;

		public PointsOfInterestController(ILogger<PointsOfInterestController> logger, IMailService mailService, ICityInfoRepository repository, IMapper mapper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_mailService = mailService ?? throw new ArgumentNullException(nameof(mailService));
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
			_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
		}

		[HttpGet]
		public async Task<ActionResult<IEnumerable<PointOfInterestDto>>> GetPointsOfInterest(int cityId)
		{
			var cityName = User.Claims.FirstOrDefault(c => c.Type == "city")?.Value;

			if (!await _repository.CityNameMatchesCityId(cityName, cityId))
			{
				return Forbid();
			}

			if (!await _repository.CityExistsAsync(cityId))
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			var pointsOfInterestForCity = await _repository.GetPointsOfInterestForCityAsync(cityId);

			return Ok(_mapper.Map<IEnumerable<PointOfInterestDto>>(pointsOfInterestForCity));
		}

		[HttpGet("{pointofinterestid}", Name = "GetPointOfInterest")]
		public async Task<ActionResult<PointOfInterestDto>> GetPointOfInterest(int cityId, int pointOfInterestId)
		{
			if (!await _repository.CityExistsAsync(cityId))
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}


			var pointOfInterest = await _repository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);

			if (pointOfInterest == null)
			{
				return NotFound();
			}

			return Ok(_mapper.Map<PointOfInterestDto>(pointOfInterest));
		}

		[HttpPost]
		public async Task<ActionResult<PointOfInterestDto>> CreatePointOfInterest(int cityId, [FromBody] PointOfInterestForCreationDto pointOfInterest)
		{
			if (!await _repository.CityExistsAsync(cityId))
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			var finalPointOfInterest = _mapper.Map<Entities.PointOfInterest>(pointOfInterest);

			await _repository.AddPointOfInterestForCityAsync(cityId, finalPointOfInterest);

			await _repository.SaveChangesAsync();

			var createdPointOfInterestToReturn = _mapper.Map<Models.PointOfInterestDto>(finalPointOfInterest);

			return CreatedAtRoute("GetPointOfInterest", new { cityId = cityId, pointOfInterestId = createdPointOfInterestToReturn.Id }, createdPointOfInterestToReturn);
		}

		[HttpPut("{pointofinterestId}")]
		public async Task<ActionResult> UpdatePointOfInterest(int cityId, int pointOfInterestId, [FromBody] PointOfInterestForUpdateDto pointOfInterest)
		{
			if (!await _repository.CityExistsAsync(cityId))
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			var pointOfInterestEntity = await _repository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
			if (pointOfInterestEntity == null)
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			_mapper.Map(pointOfInterest, pointOfInterestEntity);

			await _repository.SaveChangesAsync();

			return NoContent();
		}

		[HttpPatch("{pointofinterestId}")]
		public async Task<ActionResult> PartiallyUpdatePointOfInterest(int cityId, int pointOfInterestId, [FromBody] JsonPatchDocument<PointOfInterestForUpdateDto> patchDocument)
		{
			if (!await _repository.CityExistsAsync(cityId))
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			var pointOfInterestEntity = await _repository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
			if (pointOfInterestEntity == null)
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			var pointOfInterestToPatch = _mapper.Map<PointOfInterestForUpdateDto>(pointOfInterestEntity);

			patchDocument.ApplyTo(pointOfInterestToPatch, ModelState);

			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			if (!TryValidateModel(pointOfInterestToPatch))
			{
				return BadRequest(ModelState);
			}

			_mapper.Map(pointOfInterestToPatch, pointOfInterestEntity);

			await _repository.SaveChangesAsync();

			return NoContent();
		}

		[HttpDelete("{pointOfInterestId}")]
		public async Task<ActionResult> DeletePointOfInterest(int cityId, int pointOfInterestId)
		{
			if (!await _repository.CityExistsAsync(cityId))
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			var pointOfInterestEntity = await _repository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
			if (pointOfInterestEntity == null)
			{
				_logger.LogInformation($"City with id {cityId} wasn't found when accessing points of interest.");
				return NotFound();
			}

			_repository.DeletePointOfInterest(pointOfInterestEntity);
			await _repository.SaveChangesAsync();

			_mailService.Send("Point of interest deleted.", $"Point of interest {pointOfInterestEntity.Name} with id {pointOfInterestEntity.Id} was deleted.");

			return NoContent();
		}
	}
}