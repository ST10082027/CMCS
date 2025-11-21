using Microsoft.VisualStudio.TestTools.UnitTesting;
using CMCS.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace CMCS.Tests
{
    [TestClass]
    public class MonthlyClaimTests
    {
        // Helper: validate a MonthlyClaim using DataAnnotations + IValidatableObject
        private static List<ValidationResult> ValidateModel(MonthlyClaim claim)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(claim);
            Validator.TryValidateObject(claim, context, results, validateAllProperties: true);
            return results;
        }

        // Helper: a simple "good" claim which should normally have no validation errors
        private static MonthlyClaim CreateValidClaim()
        {
            return new MonthlyClaim
            {
                IcUserId = "lecturer-1",
                MonthKey = "2025-03",
                Hours = 10m,
                Rate = 100m,
                Status = ClaimStatus.Draft,
                SubmittedAt = null
            };
        }

        // 1
        [TestMethod]
        public void BuildMonthKey_SingleDigitMonth_IsZeroPadded()
        {
            // Act
            var result = MonthlyClaim.BuildMonthKey(2025, 3);

            // Assert
            Assert.AreEqual("2025-03", result);
        }

        // 2
        [TestMethod]
        public void BuildMonthKey_TwoDigitMonth_IsCorrect()
        {
            var result = MonthlyClaim.BuildMonthKey(2025, 11);
            Assert.AreEqual("2025-11", result);
        }

        // 3
        [TestMethod]
        public void DefaultMonthKey_MatchesYearMonthPattern()
        {
            var claim = new MonthlyClaim();

            bool matches = Regex.IsMatch(claim.MonthKey, @"^\d{4}-\d{2}$");

            Assert.IsTrue(matches, "MonthKey should be formatted as YYYY-MM.");
        }

        // 4
        [TestMethod]
        public void Amount_Computes_HoursTimesRate()
        {
            var claim = new MonthlyClaim
            {
                Hours = 5m,
                Rate = 200m
            };

            Assert.AreEqual(1000m, claim.Amount);
        }

        // 5
        [TestMethod]
        public void Amount_RoundsToTwoDecimalPlaces()
        {
            var claim = new MonthlyClaim
            {
                Hours = 1.234m,
                Rate = 100m
            };

            Assert.AreEqual(123.40m, claim.Amount);
        }

        // 6
        [TestMethod]
        public void Validate_ValidClaim_HasNoErrors()
        {
            var claim = CreateValidClaim();

            var results = ValidateModel(claim);

            Assert.AreEqual(0, results.Count, "Expected no validation errors for a valid claim.");
        }

        // 7
        [TestMethod]
        public void Validate_InvalidMonthFormat_ReturnsError()
        {
            var claim = CreateValidClaim();
            claim.MonthKey = "March-2025"; // invalid format

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "Month must be formatted as YYYY-MM."),
                "Expected invalid month format error."
            );
        }

        // 8
        [TestMethod]
        public void Validate_MonthOutOfRange_ReturnsError()
        {
            var claim = CreateValidClaim();
            claim.MonthKey = "2025-13"; // 13th month

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "Month must be between 1 and 12."),
                "Expected month out-of-range error."
            );
        }

        // 9
        [TestMethod]
        public void Validate_HoursAbove180_ReturnsError()
        {
            var claim = CreateValidClaim();
            claim.Hours = 181m;

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "Total hours for a single month may not exceed 180."),
                "Expected error when hours exceed 180."
            );
        }

        // 10
        [TestMethod]
        public void Validate_HoursEqual180_IsAllowed()
        {
            var claim = CreateValidClaim();
            claim.Hours = 180m;

            var results = ValidateModel(claim);

            Assert.IsFalse(
                results.Any(r => r.ErrorMessage == "Total hours for a single month may not exceed 180."),
                "Hours == 180 should be allowed."
            );
        }

        // 11
        [TestMethod]
        public void Validate_NegativeHours_ReturnsError()
        {
            var claim = CreateValidClaim();
            claim.Hours = -1m;

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "Hours cannot be negative."),
                "Expected negative hours error."
            );
        }

        // 12
        [TestMethod]
        public void Validate_NegativeRate_ReturnsError()
        {
            var claim = CreateValidClaim();
            claim.Rate = -1m;

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "Hourly rate cannot be negative."),
                "Expected negative rate error."
            );
        }

        // 13
        [TestMethod]
        public void Validate_NegativeAmount_ReturnsError_WhenHoursAndRateProduceNegative()
        {
            var claim = CreateValidClaim();
            claim.Hours = 1m;
            claim.Rate = -1m; // Amount = -1m

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "Calculated Amount cannot be negative."),
                "Expected negative amount error."
            );
        }

        // 14
        [TestMethod]
        public void Validate_PendingStatusWithoutSubmittedAt_ReturnsError()
        {
            var claim = CreateValidClaim();
            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = null;

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.ErrorMessage == "SubmittedAt must be set when the claim is Pending."),
                "Expected error when Pending but SubmittedAt is null."
            );
        }

        // 15
        [TestMethod]
        public void Validate_PendingStatusWithSubmittedAt_IsValidForThatRule()
        {
            var claim = CreateValidClaim();
            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = DateTime.UtcNow;

            var results = ValidateModel(claim);

            Assert.IsFalse(
                results.Any(r => r.ErrorMessage == "SubmittedAt must be set when the claim is Pending."),
                "No SubmittedAt/Status error expected when SubmittedAt is set."
            );
        }

        // 16
        [TestMethod]
        public void YearProperty_ReadsFromMonthKey()
        {
            var claim = new MonthlyClaim
            {
                IcUserId = "lecturer-1",
                MonthKey = "2024-07"
            };

            Assert.AreEqual(2024, claim.Year);
        }

        // 17
        [TestMethod]
        public void YearProperty_UpdatesMonthKey_WhenSet()
        {
            var claim = new MonthlyClaim
            {
                IcUserId = "lecturer-1",
                MonthKey = "2024-07"
            };

            claim.Year = 2025;

            Assert.AreEqual("2025-07", claim.MonthKey);
        }

        // 18
        [TestMethod]
        public void MonthProperty_ReadsFromMonthKey()
        {
            var claim = new MonthlyClaim
            {
                IcUserId = "lecturer-1",
                MonthKey = "2024-11"
            };

            Assert.AreEqual(11, claim.Month);
        }

        // 19
        [TestMethod]
        public void MonthProperty_UpdatesMonthKey_WhenSet()
        {
            var claim = new MonthlyClaim
            {
                IcUserId = "lecturer-1",
                MonthKey = "2024-03"
            };

            claim.Month = 12;

            Assert.AreEqual("2024-12", claim.MonthKey);
        }

        // 20
        [TestMethod]
        public void IcUserId_IsRequired()
        {
            var claim = CreateValidClaim();
            claim.IcUserId = null!; // missing

            var results = ValidateModel(claim);

            Assert.IsTrue(
                results.Any(r => r.MemberNames.Contains(nameof(MonthlyClaim.IcUserId))),
                "Expected validation error on IcUserId when null."
            );
        }

        // 21
        [TestMethod]
        public void PeriodLabel_ReturnsMonthKey()
        {
            var claim = new MonthlyClaim
            {
                IcUserId = "lecturer-1",
                MonthKey = "2025-06"
            };

            Assert.AreEqual(claim.MonthKey, claim.PeriodLabel);
        }
    }
}
