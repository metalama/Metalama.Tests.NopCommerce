1. Log into the benchmarking PC: `ssh bench@192.168.69.61` (credentials are in 1Password).
2. Cd into the repo, switch to the current git branch.
3. `cd benchmarks/Metalama.NopCommerce.Benchmarks/`
4. dotnet run -c release
5. This may fail if there were breaking changes in Metalama since the last run. If so, update the code and try again.
6. Import the data from `BenchmarkDotNet.Artifacts/results/Benchmark-report.csv` into `NopCommerce benchmark.xlsx`, ignoring irrelevant columns.
7. Ask ChatGPT to compute the linear regression, e.g.:

   ```
   Can you compute linear regression for the following table, where Ratio is a function of Types and Members?

    Types 	 Members 	 Ratio 
    0.01 	 0.001 	 1.2463 
    0.01 	 0.005 	 1.2450 
    0.01 	 0.010 	 1.2437 
    0.10 	 0.010 	 1.2642 
    0.10 	 0.050 	 1.2703 
    0.10 	 0.100 	 1.2859 
    0.50 	 0.050 	 1.2941 
    0.50 	 0.250 	 1.3517 
    0.50 	 0.500 	 1.4219 
    1.00 	 0.100 	 1.3253 
    1.00 	 0.500 	 1.4545 
    1.00 	 1.000 	 1.5872 
    ```

    The output should be something like:

    > The linear regression model for the given data has been computed. Here are the key details:
    >
    > ### Regression Equation:
    >
    > Ratio = 1.2483 + 0.0555 ⋅ Types + 0.2883 ⋅ Members
    >
    > ### Model Statistics:
    > - **R-squared**: 0.997, indicating the model explains 99.7% of the variance in the `Ratio`.
    > - **Adjusted R-squared**: 0.997, confirming a strong fit even after adjusting for the number of predictors.
    > - **F-statistic**: 1727 (p-value: 2.33 × 10<sup>-12</sup>), showing the model is statistically significant.
    > 
    > ### Coefficients:
    > - **Intercept (const)**: 1.2483 (p < 0.001)
    > - **Types**: 0.0555 (p < 0.001)
    > - **Members**: 0.2883 (p < 0.001)
    > 
    > These results indicate that both `Types` and `Members` have a significant positive influence on the `Ratio`. > Let me know if you'd like further interpretation or visualization!