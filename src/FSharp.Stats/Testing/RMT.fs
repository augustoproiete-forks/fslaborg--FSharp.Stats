namespace FSharp.Stats.Testing


module RMT =

    open FSharp.Stats

    let spectralUnfolding egvalues =  
        let x = [0. .. 0.01 .. 1.] 
        let y = 
            egvalues
            |> Quantile.computePercentiles (Quantile.OfSorted.compute) x
        
        Interpolation.Approximation.approx x y egvalues (Seq.min)
        
    let private computeBandwidth (arr:float[]) quantile =
        let sortArr = Array.sort arr
        let computedQuantile = (int ((float arr.Length) * quantile))
        Array.init computedQuantile (fun i -> sortArr.[(arr.Length-computedQuantile)/2 + i])
        |> Distributions.Bandwidth.nrd0


    let private evaluateWigner (xValue:float) =
        System.Math.PI*0.5*xValue*System.Math.E**(-System.Math.PI*0.25*xValue*xValue)    

    let private nearesNeighbourSpacing egvLength (uEgv:seq<float>) = 
        uEgv
        |> Seq.map (fun x -> x * float egvLength)
        |> Seq.sort
        |> Seq.windowed 2
        |> Seq.map (fun a -> a.[1] - a.[0])

    let computeChiSquared (bwQuantile : float)  (m:float[,]) =
                
        let unfoldedEgv = 
            m             
            |> Algebra.EVD.symmetricEvd
            |> Algebra.EVD.getRealEigenvalues
            |> spectralUnfolding
            |> Seq.toArray

        let nnSpacing =
            unfoldedEgv
            |> nearesNeighbourSpacing unfoldedEgv.Length
            |> Seq.toArray
        
        let histo =
            let bw = computeBandwidth nnSpacing bwQuantile
            FSharp.Stats.Distributions.Frequency.create bw nnSpacing
            |> FSharp.Stats.Distributions.Frequency.getZip
            |> Seq.filter (fun (x,count) -> evaluateWigner x > 1. / ((float nnSpacing.Length)*bw)) // WICHTIG
            |> Seq.map (fun (x,y) -> x , (float y) / ((float nnSpacing.Length)*bw))
            |> Seq.toArray

        let chiSquared = 
            let wignerSurmise = Array.map (fst >> evaluateWigner) histo
            Array.map snd histo
            |> FSharp.Stats.Testing.ChiSquareTest.compute (histo.Length-1) wignerSurmise

        chiSquared

        
    ///Computes the critical Threshold for which the NNSD of the matrix significantly abides from the Wigner-Surmise
    // bwQuantile uses % data to calculate a more robust histogram
    let compute (bwQuantile : float) accuracy (sigCriterion : float) (m:float[,]) =

        let rec stepSearch left right =
            let thr = left + right / 2.
            let chi =
                m
                |> Array2D.map (fun v -> if abs v > thr then v else 0.)
                |> computeChiSquared bwQuantile

            if left - right > accuracy then 
                if chi.PValue <= sigCriterion  then
                    // jump left
                    stepSearch  left thr
                else
                    stepSearch thr right
            else
                thr,chi
                
        stepSearch 0. 1.



                
            






        