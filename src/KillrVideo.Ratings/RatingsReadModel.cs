﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Cassandra;
using KillrVideo.Ratings.Dtos;
using KillrVideo.Utils;

namespace KillrVideo.Ratings
{
    /// <summary>
    /// Handles reading data from Cassandra for videos.
    /// </summary>
    public class RatingsReadModel : IRatingsReadModel
    {
        private readonly ISession _session;

        private readonly AsyncLazy<PreparedStatement> _getVideoRating;
        private readonly AsyncLazy<PreparedStatement> _getVideoRatingForUser;
        
        public RatingsReadModel(ISession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            _session = session;

            // Reusable prepared statements
            _getVideoRating = new AsyncLazy<PreparedStatement>(() => _session.PrepareAsync("SELECT * FROM video_ratings WHERE videoid = ?"));
            _getVideoRatingForUser = new AsyncLazy<PreparedStatement>(() => _session.PrepareAsync(
                "SELECT rating FROM video_ratings_by_user WHERE videoid = ? AND userid = ?"));
        }
        
        /// <summary>
        /// Gets the current rating stats for the specified video.
        /// </summary>
        public async Task<VideoRating> GetRating(Guid videoId)
        {
            PreparedStatement preparedStatement = await _getVideoRating;
            BoundStatement boundStatement = preparedStatement.Bind(videoId);
            RowSet rows = await _session.ExecuteAsync(boundStatement);

            // Use SingleOrDefault here because it's possible a video doesn't have any ratings yet and thus has no record
            return MapRowToVideoRating(rows.SingleOrDefault(), videoId);
        }

        /// <summary>
        /// Gets the rating given by a user for a specific video.  Will return 0 for the rating if the user hasn't rated the video.
        /// </summary>
        public async Task<UserVideoRating> GetRatingFromUser(Guid videoId, Guid userId)
        {
            PreparedStatement preparedStatement = await _getVideoRatingForUser;
            BoundStatement boundStatement = preparedStatement.Bind(videoId, userId);
            RowSet rows = await _session.ExecuteAsync(boundStatement);

            // We may or may not have a rating
            Row row = rows.SingleOrDefault();
            return new UserVideoRating
            {
                VideoId = videoId, 
                UserId = userId, 
                Rating = row == null ? 0 : row.GetValue<int>("rating")
            };
        }
        
        /// <summary>
        /// Maps a row to a VideoRating object.
        /// </summary>
        private static VideoRating MapRowToVideoRating(Row row, Guid videoId)
        {
            // If we get null, just return an object with 0s as the rating tallys
            if (row == null)
                return new VideoRating {VideoId = videoId, RatingsCount = 0, RatingsTotal = 0};

            return new VideoRating
            {
                VideoId = videoId,
                RatingsCount = row.GetValue<long>("rating_counter"),
                RatingsTotal = row.GetValue<long>("rating_total")
            };
        }
    }
}