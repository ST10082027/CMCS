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
        /*This test checks that when the system generates a month key for a single-digit month, it correctly adds a leading zero (e.g., “2025-03”) to meet the required YYYY-MM format.*/
        [TestMethod]
        public void BuildMonthKey_SingleDigitMonth_IsZeroPadded()
        {
            // Act
            var result = MonthlyClaim.BuildMonthKey(2025, 3);

            // Assert
            Assert.AreEqual("2025-03", result);
        }

        // 2
        /*This test ensures that when the month already has two digits (e.g., 11), the method still produces a correctly formatted month key in the standard YYYY-MM pattern.*/
        [TestMethod]
        public void BuildMonthKey_TwoDigitMonth_IsCorrect()
        {
            var result = MonthlyClaim.BuildMonthKey(2025, 11);
            Assert.AreEqual("2025-11", result);
        }

        // 3
        /*This test verifies that a new MonthlyClaim object automatically initializes its MonthKey using the correct YYYY-MM format, matching the required pattern via a regular expression.*/
        [TestMethod]
        public void DefaultMonthKey_MatchesYearMonthPattern()
        {
            var claim = new MonthlyClaim();

            bool matches = Regex.IsMatch(claim.MonthKey, @"^\d{4}-\d{2}$");

            Assert.IsTrue(matches, "MonthKey should be formatted as YYYY-MM.");
        }

        // 4
        /*This test confirms that the Amount property correctly calculates the total claim value by multiplying the number of hours by the hourly rate.*/
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
        /*This test checks that the calculated claim amount is always rounded to two decimal places, ensuring currency formatting consistency*/
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
        /*This test ensures that a fully valid claim passes model validation with zero errors, confirming that normal, compliant claims are accepted.*/
        [TestMethod]
        public void Validate_ValidClaim_HasNoErrors()
        {
            var claim = CreateValidClaim();

            var results = ValidateModel(claim);

            Assert.AreEqual(0, results.Count, "Expected no validation errors for a valid claim.");
        }

        // 7
        /*This test makes sure that the system rejects invalid MonthKey formats (like “March-2025”) by generating a validation error for incorrect formatting.*/
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
        /*This test verifies that the claim validation catches impossible months such as “2025-13,” enforcing the rule that months must be between 1 and 12.*/
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
        /*This test confirms that the system enforces the POE rule limiting claimable hours to a maximum of 180 per month, flagging anything higher with a validation error.*/
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
        /*This test ensures that exactly 180 hours — the maximum allowed — is still considered valid and does not produce any validation errors.*/
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
        /*This test checks that the system correctly rejects claims with negative hours, ensuring that only realistic hour entries are accepted.*/
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
        /*This test checks that the system correctly rejects claims with negative hours, ensuring that only realistic hour entries are accepted.*/
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
        /*This test checks that the system prevents negative claim totals, even if the user enters a negative rate that would mathematically result in a negative amount.*/
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
        /*This test ensures that when a claim is marked as Pending, it must have a submission timestamp (SubmittedAt), enforcing proper workflow rules.*/
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
        /*This test confirms that if a pending claim does include a valid submission date, it passes the submission-related validation rule.*/
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
        /*This test verifies that the Year property correctly interprets and returns the four-digit year extracted from the MonthKey.*/
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
        /*This test ensures that changing the Year property updates the underlying MonthKey accordingly, keeping the two in sync.*/
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
        /*This test checks that the Month property correctly reads and returns the numeric month value from the MonthKey.*/
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
        /*This test checks that the Month property correctly reads and returns the numeric month value from the MonthKey.*/
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
        /*This test ensures that the claim’s IcUserId (the lecturer/contractor who submitted the claim) cannot be empty and must be provided for the claim to be valid.*/
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
        /*This test verifies that the PeriodLabel property returns the same value as the MonthKey, ensuring consistent period display across the system.*/
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
